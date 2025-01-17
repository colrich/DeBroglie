﻿using DeBroglie.Constraints;
using DeBroglie.Models;
using DeBroglie.Rot;
using DeBroglie.Topo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeBroglie.Console.Config
{
    /// <summary>
    /// Utility for constructing DeBroglie classes from the coresponding config classes.
    /// </summary>
    public class Factory
    {
        public DeBroglieConfig Config { get; set; }

        public IDictionary<string, Tile> TilesByName { get; set; }

        public Func<string, Tile> TileParser { get; set; }

        private Direction ParseDirection(string s)
        {
            switch (s.ToLower())
            {
                case "x+": return Direction.XPlus;
                case "x-": return Direction.XMinus;
                case "y+": return Direction.YPlus;
                case "y-": return Direction.YMinus;
                case "z+": return Direction.ZPlus;
                case "z-": return Direction.ZMinus;
                case "w+": return Direction.WPlus;
                case "w-": return Direction.WMinus;
            }

            if (!Enum.TryParse(s, true, out Direction r))
            {
                throw new ConfigurationException($"Unable to parse direction \"{s}\"");
            }
            return r;
        }

        private Axis ParseAxis(string s)
        {
            if (!Enum.TryParse(s, true, out Axis r))
            {
                throw new ConfigurationException($"Unable to parse axis \"{s}\"");
            }
            return r;
        }

        public Tile Parse(string s)
        {
            if (s.Contains("!"))
            {
                // TODO: Cleanup and validate
                var a = s.Split('!');
                var b = a[1];
                var refl = false;
                if (b.StartsWith("x"))
                {
                    refl = true;
                    b = b.Substring(1);
                }
                var rotateCw = (int.Parse(b) + 360) % 360;
                return new Tile(new RotatedTile
                {
                    Tile = Parse(a[0]),
                    Rotation = new Rotation(rotateCw, refl),
                });
            }

            if (TilesByName.TryGetValue(s, out var tile))
            {
                return tile;
            }
            if (TileParser != null)
            {
                return TileParser(s);
            }
            else
            {
                return new Tile(s);
            }
        }

        public Topology GetOutputTopology(DirectionSet directions)
        {
            var is3d = directions.Type == DirectionSetType.Cartesian3d;
            return new Topology(directions, Config.Width, Config.Height, is3d ? Config.Depth : 1, Config.PeriodicX, Config.PeriodicY, Config.PeriodicZ);
        }

        public TileRotation GetTileRotation(TileRotationTreatment? rotationTreatment, Topology topology)
        {
            var tileData = Config.Tiles;

            var tileRotationBuilder = new TileRotationBuilder(Config.RotationalSymmetry, Config.ReflectionalSymmetry, rotationTreatment ?? TileRotationTreatment.Unchanged);
            var rotationGroup = tileRotationBuilder.RotationGroup;

            // Setup tiles
            if (tileData != null)
            {
                foreach (var td in tileData)
                {
                    var tile = Parse(td.Value);
                    if (td.TileSymmetry != null)
                    {
                        var ts = TileSymmetryUtils.Parse(td.TileSymmetry);
                        tileRotationBuilder.AddSymmetry(tile, ts);
                    }
                    if (td.ReflectX != null)
                    {
                        tileRotationBuilder.Add(tile, new Rotation(0, true), Parse(td.ReflectX));
                    }
                    if (td.ReflectY != null)
                    {
                        tileRotationBuilder.Add(tile, new Rotation(180, true), Parse(td.ReflectY));
                    }
                    if (td.RotateCw != null)
                    {
                        tileRotationBuilder.Add(tile, new Rotation(rotationGroup.SmallestAngle, false), Parse(td.RotateCw));
                    }
                    if (td.RotateCcw != null)
                    {
                        tileRotationBuilder.Add(tile, new Rotation(360 - rotationGroup.SmallestAngle, false), Parse(td.RotateCcw));
                    }
                    if (td.RotationTreatment != null)
                    {
                        tileRotationBuilder.SetTreatment(tile, td.RotationTreatment.Value);
                    }
                }
            }

            return tileRotationBuilder.Build();
        }

        private void SetupAdjacencies(TileModel model, TileRotation tileRotation)
        {
            if (Config.Adjacencies != null)
            {
                var adjacentModel = model as AdjacentModel;
                if (adjacentModel == null)
                {
                    throw new ConfigurationException("Setting adjacencies is only supported for the \"adjacent\" model.");
                }

                foreach (var a in Config.Adjacencies)
                {
                    var srcAdj = a.Src.Select(Parse).Select(tileRotation.Canonicalize).ToList();
                    var destAdj = a.Dest.Select(Parse).Select(tileRotation.Canonicalize).ToList();
                    adjacentModel.AddAdjacency(srcAdj, destAdj, a.X, a.Y, a.Z, tileRotation);
                }

                // If there are no samples, set frequency to 1 for everything mentioned in this block
                foreach (var tile in adjacentModel.Tiles)
                {
                    adjacentModel.SetFrequency(tile, 1, tileRotation);
                }
            }
        }

        private void SetupTiles(TileModel model, TileRotation tileRotation)
        {
            if (Config.Tiles != null)
            {
                foreach (var tile in Config.Tiles)
                {
                    var value = Parse(tile.Value);
                    if (tile.MultiplyFrequency != null)
                    {
                        var cf = tile.MultiplyFrequency.Trim();
                        double cfd;
                        if (cf.EndsWith("%"))
                        {
                            cfd = double.Parse(cf.TrimEnd('%')) / 100;
                        }
                        else
                        {
                            cfd = double.Parse(cf);
                        }
                        model.MultiplyFrequency(value, cfd, tileRotation);
                    }
                }
            }
        }

        public TileModel GetModel(DirectionSet directions, ITopoArray<Tile>[] samples, TileRotation tileRotation)
        {
            var modelConfig = Config.Model ?? new Adjacent();
            TileModel tileModel;
            if (modelConfig is Overlapping overlapping)
            {
                var model = new OverlappingModel(overlapping.NX, overlapping.NY, overlapping.NZ);
                foreach (var sample in samples)
                {
                    model.AddSample(sample, tileRotation);
                }
                tileModel = model;
            }
            else if (modelConfig is Adjacent adjacent)
            {
                var model = new AdjacentModel(directions);
                foreach (var sample in samples)
                {
                    model.AddSample(sample, tileRotation);
                }
                tileModel = model;
            }
            else
            {
                throw new ConfigurationException($"Unrecognized model type {modelConfig.GetType()}");
            }

            SetupAdjacencies(tileModel, tileRotation);
            SetupTiles(tileModel, tileRotation);

            return tileModel;
        }

        public List<ITileConstraint> GetConstraints(DirectionSet directions, TileRotation tileRotation)
        {
            var is3d = directions.Type == DirectionSetType.Cartesian3d;

            var constraints = new List<ITileConstraint>();
            if (Config.Ground != null)
            {
                var groundTile = Parse(Config.Ground);
                constraints.Add(new BorderConstraint
                {
                    Sides = is3d ? BorderSides.ZMin : BorderSides.YMax,
                    Tiles = new[] { groundTile },
                });
                constraints.Add(new BorderConstraint
                {
                    Sides = is3d ? BorderSides.ZMin : BorderSides.YMax,
                    Tiles = new[] { groundTile },
                    InvertArea = true,
                    Ban = true,
                });
            }

            if (Config.Constraints != null)
            {
                foreach (var constraint in Config.Constraints)
                {
                    if (constraint is PathConfig pathData)
                    {
                        var tiles = new HashSet<Tile>(pathData.Tiles.Select(Parse));
                        var p = new PathConstraint(tiles, pathData.EndPoints);
                        constraints.Add(p);
                    }
                    else if (constraint is EdgedPathConfig edgedPathData)
                    {
                        var exits = edgedPathData.Exits.ToDictionary(
                            kv => Parse(kv.Key), x => (ISet<Direction>)new HashSet<Direction>(x.Value.Select(ParseDirection)));
                        var p = new EdgedPathConstraint(exits, edgedPathData.EndPoints, tileRotation);
                        constraints.Add(p);
                    }
                    else if (constraint is BorderConfig borderData)
                    {
                        var tiles = borderData.Tiles.Select(Parse).ToArray();
                        var sides = borderData.Sides == null ? BorderSides.All : (BorderSides)Enum.Parse(typeof(BorderSides), borderData.Sides, true);
                        var excludeSides = borderData.ExcludeSides == null ? BorderSides.None : (BorderSides)Enum.Parse(typeof(BorderSides), borderData.ExcludeSides, true);
                        if (!is3d)
                        {
                            sides = sides & ~BorderSides.ZMin & ~BorderSides.ZMax;
                            excludeSides = excludeSides & ~BorderSides.ZMin & ~BorderSides.ZMax;
                        }
                        constraints.Add(new BorderConstraint
                        {
                            Tiles = tiles,
                            Sides = sides,
                            ExcludeSides = excludeSides,
                            InvertArea = borderData.InvertArea,
                            Ban = borderData.Ban,
                        });
                    }
                    else if (constraint is FixedTileConfig fixedTileConfig)
                    {
                        constraints.Add(new FixedTileConstraint
                        {
                            Tiles = fixedTileConfig.Tiles.Select(Parse).ToArray(),
                            Point = fixedTileConfig.Point,
                        });
                    }
                    else if (constraint is MaxConsecutiveConfig maxConsecutiveConfig)
                    {
                        var axes = maxConsecutiveConfig.Axes?.Select(ParseAxis);
                        constraints.Add(new MaxConsecutiveConstraint
                        {
                            Tiles = new HashSet<Tile>(maxConsecutiveConfig.Tiles.Select(Parse)),
                            MaxCount = maxConsecutiveConfig.MaxCount,
                            Axes = axes == null ? null : new HashSet<Axis>(axes),
                        });
                    }
                    else if (constraint is MirrorConfig mirrorConfig)
                    {
                        constraints.Add(new MirrorConstraint
                        {
                            TileRotation = tileRotation,
                        });
                    }
                    else
                    {
                        throw new NotImplementedException($"Unknown constraint type {constraint.GetType()}");
                    }
                }
            }

            return constraints;
        }

    }
}
