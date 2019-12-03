
namespace Utilities.BimDataProcess
{
    public class BimWall
    {
        public string Id { get; set; }
        public float Thickness { get; set; }
        public BimContour Contour { get; set; }
        public BimSlice[] Slices { get; set; }
        public BimHole[] Holes { get; set; }
        public BimRebar[] Rebars { get; set; }
        public BimRebarMount[] RebarMounts { get; set; }
        public BimCone[] Cones { get; set; }
        public BimTileFace[] TilingFaces { get; set; }
    }

    public class BimSlab
    {
        public string Id { get; set; }
        public float MaxWidth { get; set; }
        public float MaxHeight { get; set; }
        public BimPoint[] ExternalContour { get; set; }
        public BimPoint[] InternalContour { get; set; }
        public BimTileFace[] TilingFaces { get; set; }
        public BimLatticeHole[] Holes { get; set; }
        public BimRebarMesh[] RebarMeshes { get; set; }
        public BimLatticeMesh[] LatticeMeshes { get; set; }
    }

    public class BimPillar
    {
        public string Id { get; set; }
        public int CoreWidth { get; set; }
        public int CoreThickness { get; set; }
        public int CoreHeight { get; set; }
        public string PillarType { get; set; }
        public BimStirrupTypes[] StirrupTypes { get; set; }
        public BimCage[] Cages { get; set; }
    }

    #region Slice

    public class BimSlice
    {
        public int Column { get; set; }
        public string Id { get; set; }
        public BimContour Contour { get; set; }
        public BimGlueSegment[] Gluesegments { get; set; }
        public BimHole[] Holes { get; set; }
    }

    public class BimHole
    {
        public string Id { get; set; }
        public BimPoint3D Normal { get; set; }
        public float Depth { get; set; }
        public BimContour Contour { get; set; }
    }

    public class BimGlueSegment
    {
        public BimPoint Start { get; set; }
        public BimPoint End { get; set; }
    }

    public class BimContour
    {
        public BimPoint[] Points { get; set; }
    }

    public class BimPoint
    {
        public float X { get; set; }
        public float Y { get; set; }
    }

    public class BimPoint3D
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
    }

    #endregion Slice

    #region Tile

    public class BimTileFace
    {
        public BimTilePlacement Placement { get; set; }
        public BimTile[] Tiles { get; set; } 
    }

    public class BimTilePlacement
    {
        public BimPoint3D Origin { get; set; }
        public BimPoint3D Normal { get; set; }
    }

    public class BimTile
    {
        public string Id { get; set; }
        public BimContour Contour { get; set; }
    }

    #endregion Tile

    #region Rebar

    public class BimRebar
    {
        public string Id { get; set; }
        public BimPoint3D StartPoint { get; set; }
        public bool StartThreading { get; set; }
        public BimPoint3D EndPoint { get; set; }
        public bool EndThreading { get; set; }
        public float Length { get; set; }
        public float Diameter { get; set; }
        public BimPoint3D Direction { get; set; }
    }

    public class BimRebarMount
    {
        public string Id { get; set; }
        public BimPoint3D StartPoint { get; set; }
        public BimPoint3D Direction { get; set; }
        public float Depth { get; set; }
        public bool Polarity { get; set; }
        public bool Orientation { get; set; }
        public string Rebar { get; set; }
    }
    public class BimCone
    { 
        public string Id { get; set; }
        public BimPoint3D StartPoint { get; set; }
        public BimPoint3D Direction { get; set; }
        public float Depth { get; set; }
    }

    public class BimRebarMesh
    {
        public string Id { get; set; }
        public BimRebar[] Rebars { get; set; }
    }

    #endregion Rebar

    #region Lattice

    public class BimLattice
    {
        public string Id { get; set; }
        public BimPoint3D StartPoint { get; set; }
        public BimPoint3D EndPoint { get; set; }
        public float Length { get; set; }
        public float Height { get; set; }
        public float Thickness { get; set; }
    }

    public class BimLatticeMesh
    {
        public string Id { get; set; }
        public BimLattice[] Lattices { get; set; }
    }

    public class BimLatticeHole
    {
        public BimPoint[] Points { get; set; }
    }

    #endregion Lattice

    #region Pillar

    public class BimStirrupTypes
    {
        public string Id { get; set; }
        public int Diameter { get; set; }
        public BimContour Contour { get; set; }
    }

    public class BimStirrup
    {
        public string Id { get; set; }
        public int Position { get; set; }
        public string Type { get; set; }
    }

    public class BimSubCage
    {
        public string Id { get; set; }
        public BimStirrup[] Stirrups { get; set; }
        public BimRebar[] Rebars { get; set; }
    }

    public class BimCage
    {
        public string Id { get; set; }
        public BimSubCage[] SubCages { get; set; }
        public BimStirrup[] Stirrups { get; set; }
        public BimRebar[] Rebars { get; set; }
    }

    #endregion Pillar
}
