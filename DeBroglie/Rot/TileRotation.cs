﻿using System;
using System.Collections.Generic;

namespace DeBroglie.Rot
{

    /// <summary>
    /// Stores how tiles transform to each other via rotations and reflections.
    /// These are constructed with a <see cref="TileRotationBuilder"/>
    /// </summary>
    public class TileRotation
    {
        private readonly IDictionary<Tile, IDictionary<Rotation, Tile>> rotations;
        private readonly IDictionary<Tile, TileRotationTreatment> treatments;
        private readonly TileRotationTreatment defaultTreatment;
        private readonly RotationGroup rotationGroup;

        internal TileRotation(
            IDictionary<Tile, IDictionary<Rotation, Tile>> rotations,
            IDictionary<Tile, TileRotationTreatment> treatments,
            TileRotationTreatment defaultTreatment, 
            RotationGroup rotationGroup)
        {
            this.rotations = rotations;
            this.treatments = treatments;
            this.defaultTreatment = defaultTreatment;
            this.rotationGroup = rotationGroup;
        }

        internal TileRotation(TileRotationTreatment defaultTreatment = TileRotationTreatment.Unchanged)
        {
            this.treatments = new Dictionary<Tile, TileRotationTreatment>();
            this.defaultTreatment = defaultTreatment;
        }

        /// <summary>
        /// Attempts to reflect, then rotate clockwise, a given Tile.
        /// If there is a corresponding tile (possibly the same one), then it is set to result.
        /// Otherwise, false is returned.
        /// </summary>
        public bool Rotate(Tile tile, Rotation rotation, out Tile result)
        {
            if(rotationGroup != null && tile.Value is RotatedTile rt)
            {
                rotation = rotationGroup.Mul(
                    rt.Rotation,
                    rotation);
                tile = rt.Tile;
            }

            if(rotations != null && rotations.TryGetValue(tile, out var d))
            {
                if (d.TryGetValue(rotation, out result))
                    return true;
            }

            // Transform not found, apply treatment
            if (!treatments.TryGetValue(tile, out var treatment))
                treatment = defaultTreatment;
            switch (treatment)
            {
                case TileRotationTreatment.Missing:
                    result = default(Tile);
                    return false;
                case TileRotationTreatment.Unchanged:
                    result = tile;
                    return true;
                case TileRotationTreatment.Generated:
                    if (rotation.IsIdentity)
                        result = tile;
                    else
                        result = new Tile(new RotatedTile { Rotation = rotation, Tile = tile });
                    return true;
                default:
                    throw new Exception($"Unknown treatment {treatment}");
            }
        }

        /// <summary>
        /// Convenience method for calling Rotate on each tile in a list, skipping any that cannot be rotated.
        /// </summary>
        public IEnumerable<Tile> Rotate(IEnumerable<Tile> tiles, Rotation rotation)
        {
            foreach(var tile in tiles)
            {
                if(Rotate(tile, rotation, out var tile2))
                {
                    yield return tile2;
                }
            }
        }

        /// <summary>
        /// For a rotated tile, finds the canonical representation.
        /// Leaves all other tiles unchanged.
        /// </summary>
        public Tile Canonicalize(Tile t)
        {
            if(t.Value is RotatedTile rt)
            {
                if (!Rotate(rt.Tile, rt.Rotation, out var result))
                    throw new Exception($"No tile corresponds to {t}");
                return result;
            }
            else
            {
                return t;
            }
        }

    }
}