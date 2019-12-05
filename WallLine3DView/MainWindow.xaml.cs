using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using System.Windows.Shapes;
using HelixToolkit;
using HelixToolkit.Wpf;
using Microsoft.Win32;
using Utilities.BimDataProcess;

namespace WallLine3DView
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static readonly DependencyProperty WallProperty = DependencyProperty.Register(
            "WallModel", typeof(Material), typeof(MainWindow), new UIPropertyMetadata(null));

        public MainWindow()
        {
            InitializeComponent();   
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            helixViewport.Children.RemoveAt(helixViewport.Children.Count - 1);//window can be refresh after button click            
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Multiselect = false;
            openFileDialog.Title = "请选择文件";
            openFileDialog.Filter = "所有文件(*json*)|*.json*"; //设置要选择的文件的类型
            openFileDialog.ShowDialog();
            jsonFilePath.Text = openFileDialog.FileName;
            GridLinesVisual3D gridLinesVisual3D = new GridLinesVisual3D();
            gridLinesVisual3D.Length = 1000;
            gridLinesVisual3D.Width = 1000;
            gridLinesVisual3D.Thickness = 0.2;
            gridLinesVisual3D.MajorDistance = 100;
            gridLinesVisual3D.MinorDistance = 100;
            AmbientLight ambientLight = new AmbientLight();
            helixViewport.Lights.Children.Add(ambientLight);            
            helixViewport.Children.Add(gridLinesVisual3D);
            WallVisual();
        }

        public void WallVisual()
        {
            ReadJSONFile readJSONFile = new ReadJSONFile();
            readJSONFile.ReadJSONFileLocal(jsonFilePath.Text, out BimWall bimWall);
            jsonFilePath.Text = jsonFilePath.Text.Split("\\")[jsonFilePath.Text.Split("\\").Length - 1];
            ModelVisual3D wall3DModel = new ModelVisual3D();
            Model3DGroup wallModelGroup = new Model3DGroup();
            ModelVisual3D wallFinalModel = new ModelVisual3D();
            
            StLReader stLReader = new StLReader();

            //SliceModelCreater(bimWall, out ModelVisual3D wallModel);
            WallModelCreater(bimWall, out ModelVisual3D wallModel);
                        
            HoleModelCreater(bimWall, wallModel, out ModelVisual3D wallHoleModel);
            RebarSlotModelCreater(bimWall, wallModel);
            ConeModelCreater(bimWall, wallModel);
            RebarMountModelCreater(bimWall, wallModel);

            wallFinalModel.Children.Add(wallHoleModel);
            helixViewport.Children.Add(wallFinalModel);            
        }

        /// <summary>
        /// Creat wall model via ModelVisual3D
        /// </summary>
        /// <param name="bimWall"></param>
        /// <param name="wallModel"></param>
        /// <returns></returns>
        public bool WallModelCreater(BimWall bimWall, out ModelVisual3D wallModel)
        {
            /*Generate wall side surface*/
            wallModel = new ModelVisual3D();                        
            var axis = new Vector3D(0, 0, 1);
            var render = new ExtrudedVisual3D();
            
            PointCollection contour = new PointCollection();           
            
            Polygon3D topSurfacePolygon = new Polygon3D();
            Polygon3D bottomSurfacePolygon = new Polygon3D();

            for (int i = 0; i < bimWall.Contour.Points.Count(); i++)
            {
                Point contourPoint = new Point() { X = bimWall.Contour.Points[i].X, Y = bimWall.Contour.Points[i].Y};
                Point3D topSurfacePoint = new Point3D() { X = bimWall.Contour.Points[i].X, Y = bimWall.Contour.Points[i].Y, Z = bimWall.Thickness };
                Point3D bottomSurfacePoint = new Point3D() { X = bimWall.Contour.Points[i].X, Y = bimWall.Contour.Points[i].Y, Z = 0 };
                contour.Add(contourPoint);                
                topSurfacePolygon.Points.Add(topSurfacePoint);
                bottomSurfacePolygon.Points.Add(bottomSurfacePoint);
            }
            
            render.Section = contour;
            render.Path.Add(new Point3D(0, 0, 0));
            render.Path.Add(new Point3D(0, 0, bimWall.Thickness));
            render.SectionXAxis = axis;
            render.Fill = Brushes.WhiteSmoke;
            render.IsPathClosed = true;
            render.IsSectionClosed = true;

            /*Generate top and bottom surface of wall*/
            var modelGroup = new Model3DGroup();
            var meshBuilder = new MeshBuilder(false, false, false);
            
            var topSurface = topSurfacePolygon.Flatten();
            var bottomSurface = bottomSurfacePolygon.Flatten();

            var topTriangleIndexes = CuttingEarsTriangulator.Triangulate(topSurface.Points);
            var bottomTriangleIndexes = CuttingEarsTriangulator.Triangulate(bottomSurface.Points);
            meshBuilder.Append(topSurfacePolygon.Points, topTriangleIndexes);
            meshBuilder.Append(bottomSurfacePolygon.Points, bottomTriangleIndexes);
            
            var mesh = meshBuilder.ToMesh(true);
            var grayMaterial = MaterialHelper.CreateMaterial(Brushes.WhiteSmoke, 100.0f, specularPower: 50, ambient: 200);
            var insideMaterial = MaterialHelper.CreateMaterial(Brushes.WhiteSmoke, 100.0f, specularPower: 50, ambient: 200);

            modelGroup.Children.Add(new GeometryModel3D { Geometry = mesh, Material = grayMaterial, BackMaterial = insideMaterial });
            
            var visual3D = new ModelVisual3D();
            visual3D.Content = modelGroup;
            wallModel.Children.Add(render);
            wallModel.Children.Add(visual3D);
            
            return true;
        }
        /// <summary>
        /// Creat holes, which include windows and meshsteps;
        /// </summary>
        /// <param name="bimWall"></param>
        /// <param name="wallModel"></param>
        /// <returns></returns>
        public bool HoleModelCreater(BimWall bimWall, ModelVisual3D wallModel)
        {
            float EPSILON = 0.001f;
            float holeModelOffset = 0.02f;
            
            for (int hole_id = 0; hole_id < bimWall.Holes.Length; hole_id++)
            {
                if (Math.Abs(bimWall.Holes[hole_id].Normal.Y - 1) < EPSILON)
                {
                    List<Point3D> holePoints = new List<Point3D>();
                    List<double> holeContour_X = new List<double>();
                    List<double> holeContour_Y = new List<double>();
                    Point3D holeCenter = new Point3D();                    
                    for (int i = 0; i < bimWall.Holes[hole_id].Contour.Points.Length; i++)
                    {
                        Point3D holeContourPoint = new Point3D(bimWall.Holes[hole_id].Contour.Points[i].X, bimWall.Holes[hole_id].Contour.Points[i].Y, bimWall.Holes[hole_id].Depth);
                        //nextPoint = new Point3D(bimWall.Holes[hole_id].Contour.Points[(i + 1) % bimWall.Holes[hole_id].Contour.Points.Length].X, bimWall.Holes[hole_id].Contour.Points[(i + 1) % bimWall.Holes[hole_id].Contour.Points.Length].Y, bimWall.Holes[hole_id].Depth);
                        holePoints.Add(holeContourPoint);
                        holeContour_X.Add(holeContourPoint.X);
                        holeContour_Y.Add(holeContourPoint.Y);
                    }
                    holeCenter.X = holeContour_X.Average();
                    holeCenter.Y = holeContour_Y.Average();
                    if (Math.Abs(bimWall.Holes[hole_id].Depth - bimWall.Thickness) < EPSILON)
                    {
                        holeCenter.Z = bimWall.Holes[hole_id].Depth / 2;
                        wallModel.Children.Add(new BoxVisual3D
                        {
                            Fill = Brushes.Black,
                            Center = holeCenter,
                            Height = bimWall.Holes[hole_id].Depth + holeModelOffset,
                            Length = Math.Abs(holeContour_X.Max() - holeContour_X.Min()) + holeModelOffset,
                            Width = holeContour_Y.Max() - holeContour_Y.Min() + holeModelOffset
                        });
                    }
                    else
                    {
                        holeCenter.Z = (2 * bimWall.Thickness - bimWall.Holes[hole_id].Depth) / 2;
                        wallModel.Children.Add(new BoxVisual3D
                        {
                            Fill = Brushes.Black,
                            Center = holeCenter,
                            Height = bimWall.Holes[hole_id].Depth + holeModelOffset,
                            Length = Math.Abs(holeContour_X.Max() - holeContour_X.Min()) + 10 * holeModelOffset,//
                            Width = holeContour_Y.Max() - holeContour_Y.Min() + holeModelOffset
                        });
                    }
                }

                else if (Math.Abs(bimWall.Holes[hole_id].Normal.Y + 1) < EPSILON)
                {
                    List<Point3D> holePoints = new List<Point3D>();
                    List<double> holeContour_X = new List<double>();
                    List<double> holeContour_Y = new List<double>();
                    Point3D holeCenter = new Point3D();                    
                    for (int i = 0; i < bimWall.Holes[hole_id].Contour.Points.Length; i++)
                    {
                        Point3D holeContourPoint = new Point3D(bimWall.Holes[hole_id].Contour.Points[i].X, bimWall.Holes[hole_id].Contour.Points[i].Y, bimWall.Holes[hole_id].Depth);                        
                        holePoints.Add(holeContourPoint);
                        holeContour_X.Add(holeContourPoint.X);
                        holeContour_Y.Add(holeContourPoint.Y);
                    }
                    holeCenter.X = holeContour_X.Average();
                    holeCenter.Y = holeContour_Y.Average();
                    if (Math.Abs(bimWall.Holes[hole_id].Depth - bimWall.Thickness) < EPSILON)
                    {
                        holeCenter.Z = bimWall.Holes[hole_id].Depth / 2;
                        wallModel.Children.Add(new BoxVisual3D
                        {
                            Fill = Brushes.Black,
                            Center = holeCenter,
                            Height = bimWall.Holes[hole_id].Depth + holeModelOffset,
                            Length = Math.Abs(holeContour_X.Max() - holeContour_X.Min()) + holeModelOffset,
                            Width = holeContour_Y.Max() - holeContour_Y.Min() + holeModelOffset
                        });
                    }
                    else
                    {
                        holeCenter.Z = (0 + bimWall.Holes[hole_id].Depth) / 2;
                        wallModel.Children.Add(new BoxVisual3D
                        {
                            Fill = Brushes.Black,
                            Center = holeCenter,
                            Height = bimWall.Holes[hole_id].Depth + holeModelOffset,
                            Length = Math.Abs(holeContour_X.Max() - holeContour_X.Min()) + 10 * holeModelOffset,//
                            Width = holeContour_Y.Max() - holeContour_Y.Min() + holeModelOffset
                        });
                    }
                }
                else
                    throw new Exception("Json file are wrong in holes data, please recheck it!");
                
            }
            return true;
        }
        /// <summary>
        /// Creat rebar slots
        /// </summary>
        /// <param name="bimWall"></param>
        /// <param name="wallModel"></param>
        /// <returns></returns>
        public bool RebarSlotModelCreater(BimWall bimWall, ModelVisual3D wallModel)
        {
            float rebarWidth = 8;
            float EPSILON = 0.002f;
            float rebarVisualOffset = 0.08f;
            List<Point3D> startPoints = new List<Point3D>();
            
            var meshBuilder = new MeshBuilder();
            for (int rebar_id = 0; rebar_id < bimWall.Rebars.Count(); rebar_id++)//
            {
                if (Math.Abs(bimWall.Rebars[rebar_id].Direction.Y + 1) < EPSILON)
                {
                    if (MathF.Abs(bimWall.Rebars[rebar_id].StartPoint.Z - bimWall.Rebars[rebar_id].EndPoint.Z) < EPSILON)
                    {
                        Point3D startPoint = new Point3D(bimWall.Rebars[rebar_id].EndPoint.X, bimWall.Rebars[rebar_id].EndPoint.Z, bimWall.Rebars[rebar_id].EndPoint.Y);
                        startPoints.Add(startPoint);
                        wallModel.Children.Add(new BoxVisual3D
                        {
                            Fill = Brushes.Black,
                            Center = new Point3D(MathF.Abs(bimWall.Rebars[rebar_id].StartPoint.X + bimWall.Rebars[rebar_id].EndPoint.X) / 2, startPoint.Y, (bimWall.Thickness + startPoint.Z) / 2),
                            Height = bimWall.Thickness - startPoint.Z + rebarVisualOffset,
                            Length = MathF.Abs(bimWall.Rebars[rebar_id].StartPoint.X - bimWall.Rebars[rebar_id].EndPoint.X) + rebarVisualOffset * 2,
                            Width = rebarWidth + rebarVisualOffset
                        });

                        #region
                        //wallModel.IsEnabled = false;
                        //plan3Ds.Position = new Point3D(startPoint.X, startPoint.Y + rebarWidth / 2, (bimWall.Thickness + startPoint.Z) / 2);
                        //plan3Ds.Normal = new Vector3D(0, -1, 0);
                        //wallModel.CuttingPlanes.Add(plan3Ds);

                        ////wallModel.IsEnabled = true;
                        //plan3Ds = new Plane3D();
                        //plan3Ds.Position = new Point3D(startPoint.X, startPoint.Y - rebarWidth / 2, startPoint.Z);
                        //plan3Ds.Normal = new Vector3D(0, 0, 1);
                        //wallModel.CuttingPlanes.Add(plan3Ds);

                        ////wallModel.IsEnabled = true;
                        //plan3Ds = new Plane3D();
                        //plan3Ds.Position = new Point3D(startPoint.X, startPoint.Y - rebarWidth / 2, (bimWall.Thickness + startPoint.Z) / 2);
                        //plan3Ds.Normal = new Vector3D(0, 1, 0);
                        //wallModel.CuttingPlanes.Add(plan3Ds);  
                        #endregion
                    }
                    else if (MathF.Abs(bimWall.Rebars[rebar_id].StartPoint.X - bimWall.Rebars[rebar_id].EndPoint.X) < EPSILON)
                    {
                        Point3D startPoint = new Point3D(bimWall.Rebars[rebar_id].StartPoint.X, bimWall.Rebars[rebar_id].StartPoint.Z, bimWall.Rebars[rebar_id].StartPoint.Y);
                        startPoints.Add(startPoint);
                        wallModel.Children.Add(new BoxVisual3D
                        {
                            Fill = Brushes.Black,
                            Center = new Point3D(startPoint.X, MathF.Abs(bimWall.Rebars[rebar_id].StartPoint.Z + bimWall.Rebars[rebar_id].EndPoint.Z) / 2, (bimWall.Thickness + startPoint.Z) / 2),
                            Height = bimWall.Thickness - startPoint.Z + rebarVisualOffset,
                            Length = rebarWidth + rebarVisualOffset * 2,
                            Width = MathF.Abs(bimWall.Rebars[rebar_id].StartPoint.Z - bimWall.Rebars[rebar_id].EndPoint.Z) + rebarVisualOffset
                        });
                    }
                }
                else if (Math.Abs(bimWall.Rebars[rebar_id].Direction.Y - 1) < EPSILON)
                {
                    if (MathF.Abs(bimWall.Rebars[rebar_id].StartPoint.Z - bimWall.Rebars[rebar_id].EndPoint.Z) < EPSILON)
                    {
                        Point3D startPoint = new Point3D(bimWall.Rebars[rebar_id].EndPoint.X, bimWall.Rebars[rebar_id].EndPoint.Z, bimWall.Rebars[rebar_id].EndPoint.Y);
                        startPoints.Add(startPoint);
                        wallModel.Children.Add(new BoxVisual3D
                        {
                            Fill = Brushes.Black,
                            Center = new Point3D(MathF.Abs(bimWall.Rebars[rebar_id].StartPoint.X + bimWall.Rebars[rebar_id].EndPoint.X) / 2, startPoint.Y, (0 + startPoint.Z) / 2),
                            Height = startPoint.Z + rebarVisualOffset,
                            Length = MathF.Abs(bimWall.Rebars[rebar_id].StartPoint.X - bimWall.Rebars[rebar_id].EndPoint.X) + rebarVisualOffset,
                            Width = rebarWidth + rebarVisualOffset
                        });                        
                    }
                    else if (MathF.Abs(bimWall.Rebars[rebar_id].StartPoint.X - bimWall.Rebars[rebar_id].EndPoint.X) < EPSILON)
                    {
                        Point3D startPoint = new Point3D(bimWall.Rebars[rebar_id].StartPoint.X, bimWall.Rebars[rebar_id].StartPoint.Z, bimWall.Rebars[rebar_id].StartPoint.Y);
                        startPoints.Add(startPoint);
                        wallModel.Children.Add(new BoxVisual3D
                        {
                            Fill = Brushes.Black,
                            Center = new Point3D(startPoint.X, MathF.Abs(bimWall.Rebars[rebar_id].StartPoint.Z + bimWall.Rebars[rebar_id].EndPoint.Z) / 2, (0 + startPoint.Z) / 2),
                            Height = startPoint.Z + rebarVisualOffset,
                            Length = rebarWidth + rebarVisualOffset,
                            Width = MathF.Abs(bimWall.Rebars[rebar_id].StartPoint.Z - bimWall.Rebars[rebar_id].EndPoint.Z) + rebarVisualOffset
                        });
                    }
                }
                else
                    throw new Exception("Json file are wrong in rebar data, please recheck it!");
            }            
            return true;
        }
        /// <summary>
        /// Creat rebar mounts
        /// </summary>
        /// <param name="bimWall"></param>
        /// <param name="wallModel"></param>
        /// <returns></returns>
        public bool RebarMountModelCreater(BimWall bimWall, ModelVisual3D wallModel)
        {
            Vector2[] wallContour = new Vector2[bimWall.Contour.Points.Length];
            for (int i = 0; i < bimWall.Contour.Points.Length; i++)
            {
                wallContour[i] = new Vector2(bimWall.Contour.Points[i].X, bimWall.Contour.Points[i].Y);
            }
            for (int mount_id = 0; mount_id < bimWall.RebarMounts.Length; mount_id++)
            {
                ModelVisual3D rebarMount = new ModelVisual3D();
                if (IsRebarMountInside(bimWall.RebarMounts[mount_id], wallContour))
                {
                    rebarMount = RebarMount(bimWall.RebarMounts[mount_id], Math.Min(bimWall.Thickness - bimWall.RebarMounts[mount_id].StartPoint.Y, bimWall.RebarMounts[mount_id].StartPoint.Y), bimWall.Thickness, Colors.Black, true);
                }
                else
                    rebarMount = RebarMount(bimWall.RebarMounts[mount_id], Math.Min(bimWall.Thickness - bimWall.RebarMounts[mount_id].StartPoint.Y, bimWall.RebarMounts[mount_id].StartPoint.Y), bimWall.Thickness ,Colors.Black);
                
                wallModel.Children.Add(rebarMount);
            }            
            return true;
        }
        /// <summary>
        /// Creat cones model in wall model
        /// </summary>
        /// <param name="bimWall"></param>
        /// <param name="wallModel"></param>
        /// <returns></returns>
        public bool ConeModelCreater(BimWall bimWall, ModelVisual3D wallModel)
        {
            //wallModel = new ModelVisual3D();
            double coneVisualOffset = 0.01;
            double coneDiameter = 20;
            var coneMeshBuilder = new MeshBuilder(false,false);

            for (int cone_id = 0; cone_id < bimWall.Cones.Length; cone_id++)
            {
                Point3D startPoint = new Point3D(bimWall.Cones[cone_id].StartPoint.X, bimWall.Cones[cone_id].StartPoint.Z - coneVisualOffset, bimWall.Cones[cone_id].StartPoint.Y);
                Point3D endPoint = new Point3D(bimWall.Cones[cone_id].StartPoint.X, bimWall.Cones[cone_id].StartPoint.Z + bimWall.Cones[cone_id].Depth, bimWall.Cones[cone_id].StartPoint.Y);
                coneMeshBuilder.AddCylinder(startPoint, endPoint, coneDiameter, 360, true, true);
                
                var coneMesh = coneMeshBuilder.ToMesh(true);

                var coneMaterial = MaterialHelper.CreateMaterial(Colors.Black);
                Model3DGroup coneModelGroup = new Model3DGroup();
                coneModelGroup.Children.Add(new GeometryModel3D { Geometry = coneMesh, Material = coneMaterial, BackMaterial = coneMaterial });
                var coneVisual3D = new ModelVisual3D();
                coneVisual3D.Content = coneModelGroup;
                wallModel.Children.Add(coneVisual3D);
            }

            return true;
        }
        //public CuttingPlaneGroup ModelUpdate(CuttingPlaneGroup wallModel)
        //{
        //    CuttingPlaneGroup _wallModel = new CuttingPlaneGroup();
        //    _wallModel.Operation = CuttingOperation.Subtract;
        //    _wallModel.Children.Add(wallModel);
        //    return _wallModel;
        //}
        public ModelVisual3D RebarMount(BimRebarMount bimRebarMount, float rebarMountDepth, float wallThickness, Color color)
        {
            float EPSILON = 0.001f;
            double rebarMountDiameter = 20;
            double rebarMountVisualOffset = 0.5;
            double rebarMountOffset = 15;            
            
            ModelVisual3D rebarMountVisual3D = new ModelVisual3D();
            
            if (bimRebarMount.StartPoint.Y <= wallThickness / 2)
            {
                Point3D boxCenter = Math.Abs(bimRebarMount.Direction.X - 1) < EPSILON ? new Point3D(bimRebarMount.StartPoint.X + rebarMountOffset / 2 - rebarMountVisualOffset, bimRebarMount.StartPoint.Z, bimRebarMount.StartPoint.Y - rebarMountDepth / 2) : new Point3D(bimRebarMount.StartPoint.X - rebarMountOffset / 2 + rebarMountVisualOffset, bimRebarMount.StartPoint.Z, bimRebarMount.StartPoint.Y - rebarMountDepth / 2);
                Point3D cylinderCenter1 = Math.Abs(bimRebarMount.Direction.X - 1) < EPSILON ? new Point3D(bimRebarMount.StartPoint.X + rebarMountOffset, bimRebarMount.StartPoint.Z, bimRebarMount.StartPoint.Y - rebarMountVisualOffset / 2) : new Point3D(bimRebarMount.StartPoint.X - rebarMountOffset, bimRebarMount.StartPoint.Z, bimRebarMount.StartPoint.Y - rebarMountVisualOffset / 2);
                Point3D cylinderCenter2 = Math.Abs(bimRebarMount.Direction.X - 1) < EPSILON ? new Point3D(bimRebarMount.StartPoint.X + rebarMountOffset, bimRebarMount.StartPoint.Z, bimRebarMount.StartPoint.Y - rebarMountDepth - rebarMountVisualOffset / 2) : new Point3D(bimRebarMount.StartPoint.X - rebarMountOffset, bimRebarMount.StartPoint.Z, bimRebarMount.StartPoint.Y - rebarMountDepth - rebarMountVisualOffset / 2);
                var rebarMountMeshBuilder = new MeshBuilder(false, false);
                rebarMountMeshBuilder.AddBox(boxCenter, rebarMountOffset + rebarMountVisualOffset, rebarMountDiameter, rebarMountDepth + rebarMountVisualOffset);//Point3D center, double xlength, double ylength, double zlength
                rebarMountMeshBuilder.AddCylinder(cylinderCenter1, cylinderCenter2, rebarMountDiameter / 2, 360, true, true);
                var rebarMountMesh = rebarMountMeshBuilder.ToMesh(true);

                var rebarMountMaterial = MaterialHelper.CreateMaterial(Colors.Black);
                Model3DGroup rebarMountModelGroup = new Model3DGroup();
                rebarMountModelGroup.Children.Add(new GeometryModel3D { Geometry = rebarMountMesh, Material = rebarMountMaterial, BackMaterial = rebarMountMaterial });

                rebarMountVisual3D.Content = rebarMountModelGroup;
            } 
            else if (bimRebarMount.StartPoint.Y > wallThickness / 2)
            {
                Point3D boxCenter = Math.Abs(bimRebarMount.Direction.X - 1) < EPSILON ? new Point3D(bimRebarMount.StartPoint.X + rebarMountOffset / 2 - rebarMountVisualOffset, bimRebarMount.StartPoint.Z, bimRebarMount.StartPoint.Y + rebarMountDepth / 2) : new Point3D(bimRebarMount.StartPoint.X - rebarMountOffset / 2 + rebarMountVisualOffset, bimRebarMount.StartPoint.Z, bimRebarMount.StartPoint.Y + rebarMountDepth / 2);
                Point3D cylinderCenter1 = Math.Abs(bimRebarMount.Direction.X - 1) < EPSILON ? new Point3D(bimRebarMount.StartPoint.X + rebarMountOffset, bimRebarMount.StartPoint.Z, bimRebarMount.StartPoint.Y + rebarMountVisualOffset / 2) : new Point3D(bimRebarMount.StartPoint.X - rebarMountOffset, bimRebarMount.StartPoint.Z, bimRebarMount.StartPoint.Y + rebarMountVisualOffset / 2);
                Point3D cylinderCenter2 = Math.Abs(bimRebarMount.Direction.X - 1) < EPSILON ? new Point3D(bimRebarMount.StartPoint.X + rebarMountOffset, bimRebarMount.StartPoint.Z, bimRebarMount.StartPoint.Y + rebarMountDepth + rebarMountVisualOffset / 2) : new Point3D(bimRebarMount.StartPoint.X - rebarMountOffset, bimRebarMount.StartPoint.Z, bimRebarMount.StartPoint.Y + rebarMountDepth + rebarMountVisualOffset / 2);
                var rebarMountMeshBuilder = new MeshBuilder(false, false);
                rebarMountMeshBuilder.AddBox(boxCenter, rebarMountOffset, rebarMountDiameter, rebarMountDepth + rebarMountVisualOffset);//Point3D center, double xlength, double ylength, double zlength
                rebarMountMeshBuilder.AddCylinder(cylinderCenter1, cylinderCenter2, rebarMountDiameter / 2, 360, true, true);
                var rebarMountMesh = rebarMountMeshBuilder.ToMesh(true);

                var rebarMountMaterial = MaterialHelper.CreateMaterial(Colors.Black);
                Model3DGroup rebarMountModelGroup = new Model3DGroup();
                rebarMountModelGroup.Children.Add(new GeometryModel3D { Geometry = rebarMountMesh, Material = rebarMountMaterial, BackMaterial = rebarMountMaterial });

                rebarMountVisual3D.Content = rebarMountModelGroup;
            }            
            else
                throw new Exception("Json file are wrong in rebar mounts data, please recheck it!");

            return rebarMountVisual3D;
        }

        public ModelVisual3D RebarMount(BimRebarMount bimRebarMount, float rebarMountDepth, float wallThickness, Color color, bool insideContour)
        {
            float EPSILON = 0.001f;
            double rebarMountDiameter = 20 + 0.006;
            double rebarMountVisualOffset = 0.05;            

            ModelVisual3D rebarMountVisual3D = new ModelVisual3D();
            if (bimRebarMount.StartPoint.Y <= wallThickness / 2)
            {
                Point3D cylinderCenter1 = Math.Abs(bimRebarMount.Direction.X - 1) < EPSILON ? new Point3D(bimRebarMount.StartPoint.X, bimRebarMount.StartPoint.Z, bimRebarMount.StartPoint.Y - rebarMountVisualOffset) : new Point3D(bimRebarMount.StartPoint.X, bimRebarMount.StartPoint.Z, bimRebarMount.StartPoint.Y - rebarMountVisualOffset);
                Point3D cylinderCenter2 = Math.Abs(bimRebarMount.Direction.X - 1) < EPSILON ? new Point3D(bimRebarMount.StartPoint.X, bimRebarMount.StartPoint.Z, bimRebarMount.StartPoint.Y - rebarMountDepth - rebarMountVisualOffset) : new Point3D(bimRebarMount.StartPoint.X, bimRebarMount.StartPoint.Z, bimRebarMount.StartPoint.Y - rebarMountDepth - rebarMountVisualOffset);
                var rebarMountMeshBuilder = new MeshBuilder(false, false);
                rebarMountMeshBuilder.AddCylinder(cylinderCenter1, cylinderCenter2, rebarMountDiameter / 2, 360, true, true);
                var rebarMountMesh = rebarMountMeshBuilder.ToMesh(true);

                var rebarMountMaterial = MaterialHelper.CreateMaterial(Colors.Black);
                Model3DGroup rebarMountModelGroup = new Model3DGroup();
                rebarMountModelGroup.Children.Add(new GeometryModel3D { Geometry = rebarMountMesh, Material = rebarMountMaterial, BackMaterial = rebarMountMaterial });

                rebarMountVisual3D.Content = rebarMountModelGroup;
            }
            else if (bimRebarMount.StartPoint.Y > wallThickness / 2)
            {                
                Point3D cylinderCenter1 = Math.Abs(bimRebarMount.Direction.X - 1) < EPSILON ? new Point3D(bimRebarMount.StartPoint.X, bimRebarMount.StartPoint.Z, bimRebarMount.StartPoint.Y + rebarMountVisualOffset) : new Point3D(bimRebarMount.StartPoint.X, bimRebarMount.StartPoint.Z, bimRebarMount.StartPoint.Y + rebarMountVisualOffset);
                Point3D cylinderCenter2 = Math.Abs(bimRebarMount.Direction.X - 1) < EPSILON ? new Point3D(bimRebarMount.StartPoint.X, bimRebarMount.StartPoint.Z, bimRebarMount.StartPoint.Y + rebarMountDepth + rebarMountVisualOffset) : new Point3D(bimRebarMount.StartPoint.X, bimRebarMount.StartPoint.Z, bimRebarMount.StartPoint.Y + rebarMountDepth + rebarMountVisualOffset);
                var rebarMountMeshBuilder = new MeshBuilder(false, false);                
                rebarMountMeshBuilder.AddCylinder(cylinderCenter1, cylinderCenter2, rebarMountDiameter / 2, 360, true, true);
                var rebarMountMesh = rebarMountMeshBuilder.ToMesh(true);

                var rebarMountMaterial = MaterialHelper.CreateMaterial(Colors.Black);
                Model3DGroup rebarMountModelGroup = new Model3DGroup();
                rebarMountModelGroup.Children.Add(new GeometryModel3D { Geometry = rebarMountMesh, Material = rebarMountMaterial, BackMaterial = rebarMountMaterial });

                rebarMountVisual3D.Content = rebarMountModelGroup;
            }            
            else
                throw new Exception("Json file are wrong in rebar mounts data, please recheck it!");
            return rebarMountVisual3D;
        }
        
        public bool IsRebarMountInside(BimRebarMount bimRebarMount, Vector2[] wallContour)
        {
            return InPolygon(new Vector2(bimRebarMount.StartPoint.X, bimRebarMount.StartPoint.Z), wallContour, out bool boundary);
        }

        private static int dcmp(float x)
        {
            double EPSLION = 1;
            if (MathF.Abs(x) < EPSLION) return 0;
            else
                return x < 0 ? -1 : 1;
        }

        public static bool OnSegment(Vector2 P1, Vector2 P2, Vector2 Q)
        {
            float product = ((P1 - Q) * (P2 - Q)).X + ((P1 - Q) * (P2 - Q)).Y;
            float crossProduct = (P1 - Q).X * (P2 - Q).Y - (P2 - Q).X * (P1 - Q).Y;
            return dcmp(crossProduct) == 0 && dcmp(product) <= 0;
        }
        public static bool OnSegment(Vector2 P1, Vector2 P2, Vector2 Q, bool _)
        {
            Q = new Vector2(MathF.Round(Q.X, 0), MathF.Round(Q.Y, 0));
            float product = ((P1 - Q) * (P2 - Q)).X + ((P1 - Q) * (P2 - Q)).Y;
            float crossProduct = (P1 - Q).X * (P2 - Q).Y - (P2 - Q).X * (P1 - Q).Y;
            return dcmp(crossProduct) == 0 && dcmp(product) < 0;
        }
        public static bool InPolygon(Vector2 P, Vector2[] polygon, out bool boundary)
        {
            bool flag = false;
            boundary = false;
            Vector2 P1 = new Vector2(), P2 = new Vector2();
            int n = polygon.Length;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                P1 = polygon[i];
                P2 = polygon[j];
                if (OnSegment(P1, P2, P))
                {
                    boundary = true;
                    return false;
                }
                if ((dcmp(P1.Y - P.Y) > 0 != dcmp(P2.Y - P.Y) > 0) && dcmp(P.X - (P.Y - P1.Y) * (P1.X - P2.X) / (P1.Y - P2.Y) - P1.X) < 0)
                    flag = !flag;
            }
            return flag;
        }

        public bool EnlargeModelView(Model3DGroup model3DGroup)
        {
            return true;
        }

        public bool SliceModelCreater(BimWall bimWall, out  ModelVisual3D wallModel)
        {
            wallModel = new ModelVisual3D();

            Brush[] brushes = new Brush[8] { Brushes.Red, Brushes.Green, Brushes.Blue, Brushes.Yellow, Brushes.WhiteSmoke, Brushes.Purple, Brushes.Olive, Brushes.Maroon};
            Color[] colors = new Color[8] { Colors.Red, Colors.Green, Colors.Blue, Colors.Yellow, Colors.WhiteSmoke, Colors.Purple, Colors.Olive, Colors.Maroon};
            for (int slice_id = 0; slice_id < bimWall.Slices.Length; slice_id++)
            {                
                var axis = new Vector3D(0, 0, 1);
                var render = new ExtrudedVisual3D();

                PointCollection contour = new PointCollection();

                Polygon3D topSurfacePolygon = new Polygon3D();
                Polygon3D bottomSurfacePolygon = new Polygon3D();

                for (int i = 0; i < bimWall.Slices[slice_id].Contour.Points.Length; i++)
                {
                    Point contourPoint = new Point() { X = bimWall.Slices[slice_id].Contour.Points[i].X, Y = bimWall.Slices[slice_id].Contour.Points[i].Y };
                    Point3D topSurfacePoint = new Point3D() { X = bimWall.Slices[slice_id].Contour.Points[i].X, Y = bimWall.Slices[slice_id].Contour.Points[i].Y, Z = bimWall.Thickness };
                    Point3D bottomSurfacePoint = new Point3D() { X = bimWall.Slices[slice_id].Contour.Points[i].X, Y = bimWall.Slices[slice_id].Contour.Points[i].Y, Z = 0 };
                    contour.Add(contourPoint);
                    topSurfacePolygon.Points.Add(topSurfacePoint);
                    bottomSurfacePolygon.Points.Add(bottomSurfacePoint);
                }

                render.Section = contour;
                render.Path.Add(new Point3D(0, 0, 0));
                render.Path.Add(new Point3D(0, 0, bimWall.Thickness));
                render.SectionXAxis = axis;
                render.Fill = brushes[slice_id];
                render.IsPathClosed = true;
                render.IsSectionClosed = true;

                /*Generate top and bottom surface of wall*/
                var modelGroup = new Model3DGroup();
                var meshBuilder = new MeshBuilder(false, false, false);

                var topSurface = topSurfacePolygon.Flatten();
                var bottomSurface = bottomSurfacePolygon.Flatten();

                var topTriangleIndexes = CuttingEarsTriangulator.Triangulate(topSurface.Points);
                var bottomTriangleIndexes = CuttingEarsTriangulator.Triangulate(bottomSurface.Points);
                meshBuilder.Append(topSurfacePolygon.Points, topTriangleIndexes);
                meshBuilder.Append(bottomSurfacePolygon.Points, bottomTriangleIndexes);

                var mesh = meshBuilder.ToMesh(true);
                var grayMaterial = MaterialHelper.CreateMaterial(colors[slice_id]);
                var insideMaterial = MaterialHelper.CreateMaterial(colors[slice_id]);

                modelGroup.Children.Add(new GeometryModel3D { Geometry = mesh, Material = grayMaterial, BackMaterial = insideMaterial });

                var visual3D = new ModelVisual3D();
                visual3D.Content = modelGroup;

                wallModel.Children.Add(visual3D);
                wallModel.Children.Add(render);
            }            
            return true;
        }

        public bool HoleModelCreater(BimWall bimWall, ModelVisual3D wallModel, out ModelVisual3D wallHolesModel)
        {
            float EPSILON = 0.1f;
            float holeModelOffset = 0.06f;
            CuttingPlaneGroup holesModel = new CuttingPlaneGroup();
            ModelVisual3D tempWallModel = new ModelVisual3D();
            wallHolesModel = new ModelVisual3D();
            tempWallModel = wallModel;
            List<double> wallContour_X = new List<double>();
            List<double> wallContour_Y = new List<double>();
            for (int i = 0; i < bimWall.Contour.Points.Length; i++)
            {
                wallContour_X.Add(bimWall.Contour.Points[i].X);
                wallContour_Y.Add(bimWall.Contour.Points[i].Y);
            }
            var planMaterial = MaterialHelper.CreateMaterial(Brushes.LightGray, 100.0f, specularPower: 50, ambient: 200);
            var sidePlaneMaterial = MaterialHelper.CreateMaterial(Brushes.WhiteSmoke, 80, specularPower: 50, ambient: 200);
            for (int hole_id = 0; hole_id < bimWall.Holes.Length; hole_id++)
            {
                holesModel = new CuttingPlaneGroup();
                holesModel.Operation = CuttingOperation.Subtract;
                holesModel.Children.Add(tempWallModel);
                tempWallModel = new ModelVisual3D();
                Vector2[] holeContour = new Vector2[bimWall.Holes[hole_id].Contour.Points.Length];
                if (Math.Abs(bimWall.Holes[hole_id].Normal.Y - 1) < EPSILON)
                {
                    List<Point3D> holePoints = new List<Point3D>();
                    List<double> holeContour_X = new List<double>();
                    List<double> holeContour_Y = new List<double>();
                    Point3D holeCenter = new Point3D();
                    for (int i = 0; i < bimWall.Holes[hole_id].Contour.Points.Length; i++)
                    {
                        Point3D holeContourPoint = new Point3D(bimWall.Holes[hole_id].Contour.Points[i].X, bimWall.Holes[hole_id].Contour.Points[i].Y, bimWall.Holes[hole_id].Depth);
                        //nextPoint = new Point3D(bimWall.Holes[hole_id].Contour.Points[(i + 1) % bimWall.Holes[hole_id].Contour.Points.Length].X, bimWall.Holes[hole_id].Contour.Points[(i + 1) % bimWall.Holes[hole_id].Contour.Points.Length].Y, bimWall.Holes[hole_id].Depth);
                        holePoints.Add(holeContourPoint);
                        holeContour_X.Add(holeContourPoint.X);
                        holeContour_Y.Add(holeContourPoint.Y);
                        holeContour[i] = new Vector2((float)holeContourPoint.X, (float)holeContourPoint.Y);
                    }
                    holeCenter.X = holeContour_X.Average();
                    holeCenter.Y = holeContour_Y.Average();
                    if (Math.Abs(bimWall.Holes[hole_id].Depth - bimWall.Thickness) < EPSILON)
                    {
                        holeCenter.Z = bimWall.Holes[hole_id].Depth / 2;
                        Plane3D plane3Ds = new Plane3D();
                        plane3Ds.Position = new Point3D(holeCenter.X, holeCenter.Y + (holeContour_Y.Max() - holeCenter.Y) + 2 * holeModelOffset, 0);
                        plane3Ds.Normal = new Vector3D(0, -1, 0);
                        holesModel.CuttingPlanes.Add(plane3Ds);

                        plane3Ds = new Plane3D();
                        plane3Ds.Position = new Point3D(holeCenter.X, holeCenter.Y + (holeContour_Y.Min() - holeCenter.Y) - 2 * holeModelOffset, 0);
                        plane3Ds.Normal = new Vector3D(0, 1, 0);
                        holesModel.CuttingPlanes.Add(plane3Ds);

                        plane3Ds = new Plane3D();
                        plane3Ds.Position = new Point3D(holeCenter.X + (holeContour_X.Max() - holeCenter.X) + 2 * holeModelOffset, holeCenter.Y, 0);
                        plane3Ds.Normal = new Vector3D(-1, 0, 0);
                        holesModel.CuttingPlanes.Add(plane3Ds);

                        plane3Ds = new Plane3D();
                        plane3Ds.Position = new Point3D(holeCenter.X + (holeContour_X.Min() - holeCenter.X) - 2 * holeModelOffset, holeCenter.Y, 0);
                        plane3Ds.Normal = new Vector3D(1, 0, 0);
                        holesModel.CuttingPlanes.Add(plane3Ds);

                        tempWallModel.Children.Add(holesModel);
                        tempWallModel.Children.Add(new RectangleVisual3D { Origin = new Point3D(holeCenter.X, holeContour_Y.Max(), holeCenter.Z), Length = holeContour_X.Max() - holeContour_X.Min(), Width = bimWall.Thickness, Normal = new Vector3D(0, -1, 0), Material = planMaterial });
                        tempWallModel.Children.Add(new RectangleVisual3D { Origin = new Point3D(holeCenter.X, holeContour_Y.Min(), holeCenter.Z), Length = holeContour_X.Max() - holeContour_X.Min(), Width = bimWall.Thickness, Normal = new Vector3D(0, 1, 0), Material = planMaterial });
                        tempWallModel.Children.Add(new RectangleVisual3D { Origin = new Point3D(holeContour_X.Max(), holeCenter.Y, holeCenter.Z), Length = bimWall.Thickness, Width = holeContour_Y.Max() - holeContour_Y.Min(), Normal = new Vector3D(-1, 0, 0.0000001), Material = planMaterial });
                        tempWallModel.Children.Add(new RectangleVisual3D { Origin = new Point3D(holeContour_X.Min(), holeCenter.Y, holeCenter.Z), Length = bimWall.Thickness, Width = holeContour_Y.Max() - holeContour_Y.Min(), Normal = new Vector3D(1, 0, 0.0000001), Material = planMaterial });
                    }
                    else
                    {
                        holeCenter.Z = (bimWall.Thickness * 2 - bimWall.Holes[hole_id].Depth) / 2;
                        Plane3D plane3Ds = new Plane3D();
                        plane3Ds.Position = new Point3D(holeCenter.X, holeCenter.Y + (holeContour_Y.Max() - holeCenter.Y) + holeModelOffset, holeCenter.Z);
                        plane3Ds.Normal = new Vector3D(0, -1, 0);
                        holesModel.CuttingPlanes.Add(plane3Ds);

                        plane3Ds = new Plane3D();
                        plane3Ds.Position = new Point3D(holeCenter.X, holeCenter.Y + (holeContour_Y.Min() - holeCenter.Y) - holeModelOffset, holeCenter.Z);
                        plane3Ds.Normal = new Vector3D(0, 1, 0);
                        holesModel.CuttingPlanes.Add(plane3Ds);

                        plane3Ds = new Plane3D();
                        plane3Ds.Position = new Point3D(holeCenter.X + (holeContour_X.Max() - holeCenter.X) + holeModelOffset, holeCenter.Y, holeCenter.Z);
                        plane3Ds.Normal = new Vector3D(-1, 0, 0);
                        holesModel.CuttingPlanes.Add(plane3Ds);

                        plane3Ds = new Plane3D();
                        plane3Ds.Position = new Point3D(holeCenter.X + (holeContour_X.Min() - holeCenter.X) - holeModelOffset, holeCenter.Y, holeCenter.Z);
                        plane3Ds.Normal = new Vector3D(1, 0, 0);
                        holesModel.CuttingPlanes.Add(plane3Ds);

                        plane3Ds = new Plane3D();
                        plane3Ds.Position = new Point3D(holeCenter.X, holeCenter.Y, holeCenter.Z + (bimWall.Thickness - bimWall.Holes[hole_id].Depth) / 2 + holeModelOffset);
                        plane3Ds.Normal = new Vector3D(0, 0, -1);
                        holesModel.CuttingPlanes.Add(plane3Ds);

                        plane3Ds = new Plane3D();
                        plane3Ds.Position = new Point3D(holeCenter.X, holeCenter.Y, holeCenter.Z - (bimWall.Thickness - bimWall.Holes[hole_id].Depth) / 2 - holeModelOffset);
                        plane3Ds.Normal = new Vector3D(0, 0, 1);
                        holesModel.CuttingPlanes.Add(plane3Ds);

                        tempWallModel.Children.Add(holesModel);
                        tempWallModel.Children.Add(new RectangleVisual3D { Origin = new Point3D(holeCenter.X, holeCenter.Y, holeCenter.Z - (bimWall.Thickness - bimWall.Holes[hole_id].Depth) / 2 - holeModelOffset), Length = holeContour_X.Max() - holeContour_X.Min(), Width = holeContour_Y.Max() - holeContour_Y.Min(), Normal = new Vector3D(0, 0, 1), Material = planMaterial });

                        Point3D insidePoint = new Point3D();
                        List<Point3D> onSegmentPoints = new List<Point3D>();
                        bool InsidePointExist = false;
                        for (int i = 0; i < bimWall.Holes[hole_id].Contour.Points.Count(); i++)
                        {
                            if (InPolygon(new Vector2(bimWall.Holes[hole_id].Contour.Points[i].X, bimWall.Holes[hole_id].Contour.Points[i].Y), holeContour, out bool onBoundary))
                            { insidePoint = holePoints[i]; InsidePointExist = true; }
                            for (int j = 0; j < bimWall.Contour.Points.Length; j++)
                            {
                                if (OnSegment(new Vector2(bimWall.Contour.Points[j].X, bimWall.Contour.Points[j].Y), new Vector2(bimWall.Contour.Points[(j + 1) % bimWall.Contour.Points.Length].X, bimWall.Contour.Points[(j + 1) % bimWall.Contour.Points.Length].Y), new Vector2(bimWall.Holes[hole_id].Contour.Points[i].X, bimWall.Holes[hole_id].Contour.Points[i].Y), true))
                                    onSegmentPoints.Add(holePoints[i]);
                            }                            
                        }
                        if (InsidePointExist)
                        {
                            tempWallModel.Children.Add(new RectangleVisual3D { Origin = new Point3D(holeContour_X.Min(), holeCenter.Y, holeCenter.Z - (bimWall.Thickness - bimWall.Holes[hole_id].Depth) / 2 - holeModelOffset), Length = holeContour_Y.Max() - holeContour_Y.Min(), Width = bimWall.Thickness, Normal = new Vector3D(1, 0, 0), Material = planMaterial });

                        }
                        else
                        {
                            if (onSegmentPoints.Count == 1)
                            {
                                for (int s = 1; s < wallContour_X.Count; s++)
                                {
                                    if (!OnSegment(new Vector2((float)wallContour_X[s], (float)wallContour_Y[s]), new Vector2((float)wallContour_X[s - 1], (float)wallContour_Y[s - 1]), new Vector2((float)onSegmentPoints[0].X, (float)onSegmentPoints[0].Y))
                                        && !OnSegment(new Vector2((float)wallContour_X[s], (float)wallContour_Y[s]), new Vector2((float)wallContour_X[(s + 1) % wallContour_X.Count], (float)wallContour_Y[(s + 1) % wallContour_Y.Count]), new Vector2((float)onSegmentPoints[0].X, (float)onSegmentPoints[0].Y))
                                        && (Math.Abs(wallContour_X[s] - onSegmentPoints[0].X) < EPSILON || Math.Abs(wallContour_Y[s] - onSegmentPoints[0].Y) < EPSILON))
                                        onSegmentPoints.Add(new Point3D(wallContour_X[s], wallContour_Y[s], onSegmentPoints[0].Z));
                                    
                                }
                            }                            
                            //tempWallModel.Children.Add(new RectangleVisual3D { Origin = new Point3D(holeContour_X.Min(), holeCenter.Y, holeCenter.Z - (bimWall.Thickness - bimWall.Holes[hole_id].Depth) / 2 - holeModelOffset), Length = holeContour_Y.Max() - holeContour_Y.Min(), Width = bimWall.Thickness, Normal = new Vector3D(1, 0, 0), Material = planMaterial });
                            Point3D originPoint = Math.Abs(onSegmentPoints[1].X - onSegmentPoints[0].X) < EPSILON ? new Point3D(onSegmentPoints[1].X, (onSegmentPoints[1].Y + onSegmentPoints[0].Y) / 2, holeCenter.Z) : new Point3D((onSegmentPoints[1].X + onSegmentPoints[0].X) / 2, onSegmentPoints[1].Y, holeCenter.Z);
                            Vector3D normalPoint = new Vector3D();
                            if (Math.Abs(onSegmentPoints[1].X - onSegmentPoints[0].X) < EPSILON)
                                normalPoint = Math.Abs(onSegmentPoints[1].X - wallContour_X.Max()) < Math.Abs(onSegmentPoints[1].X - wallContour_X.Min()) ? new Vector3D(1, 0, 0) : new Vector3D(-1, 0, 0);
                            else
                                normalPoint = Math.Abs(onSegmentPoints[1].Y - wallContour_Y.Max()) < Math.Abs(onSegmentPoints[1].Y - wallContour_Y.Min()) ? new Vector3D(0, 1, 0) : new Vector3D(0, -1, 0);
                            tempWallModel.Children.Add(new RectangleVisual3D { Origin = originPoint, Length = Math.Max(Math.Abs(onSegmentPoints[1].X - onSegmentPoints[0].X), Math.Abs(onSegmentPoints[1].Y - onSegmentPoints[0].Y)), Width = bimWall.Holes[hole_id].Depth + holeModelOffset, Normal = normalPoint, Material = sidePlaneMaterial, BackMaterial = sidePlaneMaterial });
                        }
                    }
                    
                }

                else if (Math.Abs(bimWall.Holes[hole_id].Normal.Y + 1) < EPSILON)
                {
                    List<Point3D> holePoints = new List<Point3D>();
                    List<double> holeContour_X = new List<double>();
                    List<double> holeContour_Y = new List<double>();
                    Point3D holeCenter = new Point3D();
                    for (int i = 0; i < bimWall.Holes[hole_id].Contour.Points.Length; i++)
                    {
                        Point3D holeContourPoint = new Point3D(bimWall.Holes[hole_id].Contour.Points[i].X, bimWall.Holes[hole_id].Contour.Points[i].Y, bimWall.Holes[hole_id].Depth);
                        holePoints.Add(holeContourPoint);
                        holeContour_X.Add(holeContourPoint.X);
                        holeContour_Y.Add(holeContourPoint.Y);
                        holeContour[i] = new Vector2((float)holeContourPoint.X, (float)holeContourPoint.Y);
                    }
                    holeCenter.X = holeContour_X.Average();
                    holeCenter.Y = holeContour_Y.Average();
                    if (Math.Abs(bimWall.Holes[hole_id].Depth - bimWall.Thickness) < EPSILON)
                    {
                        holeCenter.Z = bimWall.Holes[hole_id].Depth / 2;
                        Plane3D plane3Ds = new Plane3D();
                        plane3Ds.Position = new Point3D(holeCenter.X, holeCenter.Y + (holeContour_Y.Max() - holeCenter.Y), 0);
                        plane3Ds.Normal = new Vector3D(0, -1, 0);
                        holesModel.CuttingPlanes.Add(plane3Ds);

                        plane3Ds = new Plane3D();
                        plane3Ds.Position = new Point3D(holeCenter.X, holeCenter.Y + (holeContour_Y.Min() - holeCenter.Y), 0);
                        plane3Ds.Normal = new Vector3D(0, 1, 0);
                        holesModel.CuttingPlanes.Add(plane3Ds);

                        plane3Ds = new Plane3D();
                        plane3Ds.Position = new Point3D(holeCenter.X + (holeContour_X.Max() - holeCenter.X), holeCenter.Y, 0);
                        plane3Ds.Normal = new Vector3D(-1, 0, 0);
                        holesModel.CuttingPlanes.Add(plane3Ds);

                        plane3Ds = new Plane3D();
                        plane3Ds.Position = new Point3D(holeCenter.X + (holeContour_X.Min() - holeCenter.X), holeCenter.Y, 0);
                        plane3Ds.Normal = new Vector3D(1, 0, 0);
                        holesModel.CuttingPlanes.Add(plane3Ds);

                        tempWallModel.Children.Add(holesModel);
                        tempWallModel.Children.Add(new RectangleVisual3D { Origin = new Point3D(holeCenter.X, holeContour_Y.Max(), holeCenter.Z), Length = holeContour_X.Max() - holeContour_X.Min(), Width = bimWall.Thickness, Normal = new Vector3D(0, -1, 0), Material = planMaterial });
                        tempWallModel.Children.Add(new RectangleVisual3D { Origin = new Point3D(holeCenter.X, holeContour_Y.Min(), holeCenter.Z), Length = holeContour_X.Max() - holeContour_X.Min(), Width = bimWall.Thickness, Normal = new Vector3D(0, 1, 0), Material = planMaterial });
                        tempWallModel.Children.Add(new RectangleVisual3D { Origin = new Point3D(holeContour_X.Max(), holeCenter.Y, holeCenter.Z), Length = bimWall.Thickness, Width = holeContour_Y.Max() - holeContour_Y.Min(), Normal = new Vector3D(-1, 0, 0.0000001), Material = planMaterial });
                        tempWallModel.Children.Add(new RectangleVisual3D { Origin = new Point3D(holeContour_X.Min(), holeCenter.Y, holeCenter.Z), Length = bimWall.Thickness, Width = holeContour_Y.Max() - holeContour_Y.Min(), Normal = new Vector3D(1, 0, 0.0000001), Material = planMaterial });
                    }
                    else
                    {
                        holeCenter.Z = (0 + bimWall.Holes[hole_id].Depth) / 2;                        
                        Plane3D plane3Ds = new Plane3D();
                        plane3Ds.Position = new Point3D(holeCenter.X, holeCenter.Y + (holeContour_Y.Max() - holeCenter.Y) + holeModelOffset, holeCenter.Z);
                        plane3Ds.Normal = new Vector3D(0, -1, 0);
                        holesModel.CuttingPlanes.Add(plane3Ds);

                        plane3Ds = new Plane3D();
                        plane3Ds.Position = new Point3D(holeCenter.X, holeCenter.Y + (holeContour_Y.Min() - holeCenter.Y) - holeModelOffset, holeCenter.Z);
                        plane3Ds.Normal = new Vector3D(0, 1, 0);
                        holesModel.CuttingPlanes.Add(plane3Ds);

                        plane3Ds = new Plane3D();
                        plane3Ds.Position = new Point3D(holeCenter.X + (holeContour_X.Max() - holeCenter.X) + 2 * holeModelOffset, holeCenter.Y, holeCenter.Z);
                        plane3Ds.Normal = new Vector3D(-1, 0, 0);
                        holesModel.CuttingPlanes.Add(plane3Ds);

                        plane3Ds = new Plane3D();
                        plane3Ds.Position = new Point3D(holeCenter.X + (holeContour_X.Min() - holeCenter.X) - 2 * holeModelOffset, holeCenter.Y, holeCenter.Z);
                        plane3Ds.Normal = new Vector3D(1, 0, 0);
                        holesModel.CuttingPlanes.Add(plane3Ds);

                        plane3Ds = new Plane3D();
                        plane3Ds.Position = new Point3D(holeCenter.X, holeCenter.Y, holeCenter.Z + bimWall.Holes[hole_id].Depth / 2 + holeModelOffset);
                        plane3Ds.Normal = new Vector3D(0, 0, -1);
                        holesModel.CuttingPlanes.Add(plane3Ds);

                        plane3Ds = new Plane3D();
                        plane3Ds.Position = new Point3D(holeCenter.X, holeCenter.Y, holeCenter.Z - bimWall.Holes[hole_id].Depth / 2 - holeModelOffset);
                        plane3Ds.Normal = new Vector3D(0, 0, 1);
                        holesModel.CuttingPlanes.Add(plane3Ds);

                        tempWallModel.Children.Add(holesModel);
                        tempWallModel.Children.Add(new RectangleVisual3D { Origin = new Point3D(holeCenter.X, holeCenter.Y, holeCenter.Z + bimWall.Holes[hole_id].Depth / 2 + holeModelOffset), Length = holeContour_X.Max() - holeContour_X.Min(), Width = holeContour_Y.Max() - holeContour_Y.Min(), Normal = new Vector3D(0, 0, 1), Material = planMaterial, BackMaterial = planMaterial });

                        Point3D insidePoint = new Point3D();
                        List<Point3D> onSegmentPoints = new List<Point3D>();
                        bool InsidePointExist = false;
                        for (int i = 0; i < bimWall.Holes[hole_id].Contour.Points.Count(); i++)
                        {
                            if (InPolygon(new Vector2(bimWall.Holes[hole_id].Contour.Points[i].X, bimWall.Holes[hole_id].Contour.Points[i].Y), holeContour, out bool onBoundary))
                            { insidePoint = holePoints[i]; InsidePointExist = true; }
                            for (int j = 0; j < bimWall.Contour.Points.Length; j++)
                            {
                                if (OnSegment(new Vector2(bimWall.Contour.Points[j].X, bimWall.Contour.Points[j].Y), new Vector2(bimWall.Contour.Points[(j + 1) % bimWall.Contour.Points.Length].X, bimWall.Contour.Points[(j + 1) % bimWall.Contour.Points.Length].Y), new Vector2(bimWall.Holes[hole_id].Contour.Points[i].X, bimWall.Holes[hole_id].Contour.Points[i].Y), true))
                                    onSegmentPoints.Add(holePoints[i]);
                            }
                        }
                        if (InsidePointExist)
                        {
                            tempWallModel.Children.Add(new RectangleVisual3D { Origin = new Point3D(holeContour_X.Min(), holeCenter.Y, holeCenter.Z - (bimWall.Thickness - bimWall.Holes[hole_id].Depth) / 2 - holeModelOffset), Length = holeContour_Y.Max() - holeContour_Y.Min(), Width = bimWall.Thickness, Normal = new Vector3D(1, 0, 0), Material = planMaterial });

                        }
                        else
                        {
                            if (onSegmentPoints.Count == 1)
                            {
                                for (int s = 1; s < wallContour_X.Count; s++)
                                {
                                    if (!OnSegment(new Vector2((float)wallContour_X[s], (float)wallContour_Y[s]), new Vector2((float)wallContour_X[s - 1], (float)wallContour_Y[s - 1]), new Vector2((float)onSegmentPoints[0].X, (float)onSegmentPoints[0].Y))
                                        && !OnSegment(new Vector2((float)wallContour_X[s], (float)wallContour_Y[s]), new Vector2((float)wallContour_X[(s + 1) % wallContour_X.Count], (float)wallContour_Y[(s + 1) % wallContour_Y.Count]), new Vector2((float)onSegmentPoints[0].X, (float)onSegmentPoints[0].Y))
                                        && (Math.Abs(wallContour_X[s] - onSegmentPoints[0].X) < EPSILON || Math.Abs(wallContour_Y[s] - onSegmentPoints[0].Y) < EPSILON))
                                        onSegmentPoints.Add(new Point3D(wallContour_X[s], wallContour_Y[s], onSegmentPoints[0].Z));

                                }
                            }
                            //tempWallModel.Children.Add(new RectangleVisual3D { Origin = new Point3D(holeContour_X.Min(), holeCenter.Y, holeCenter.Z - (bimWall.Thickness - bimWall.Holes[hole_id].Depth) / 2 - holeModelOffset), Length = holeContour_Y.Max() - holeContour_Y.Min(), Width = bimWall.Thickness, Normal = new Vector3D(1, 0, 0), Material = planMaterial });                            
                            Point3D originPoint = Math.Abs(onSegmentPoints[1].X - onSegmentPoints[0].X) < EPSILON ? new Point3D(onSegmentPoints[1].X, (onSegmentPoints[1].Y + onSegmentPoints[0].Y) / 2, holeCenter.Z) : new Point3D((onSegmentPoints[1].X + onSegmentPoints[0].X) / 2, onSegmentPoints[0].Y, holeCenter.Z);
                            Vector3D normalPoint = new Vector3D();
                            if (Math.Abs(onSegmentPoints[1].X - onSegmentPoints[0].X) < EPSILON)
                                normalPoint = Math.Abs(onSegmentPoints[1].X - wallContour_X.Max()) < Math.Abs(onSegmentPoints[1].X - wallContour_X.Min()) ? new Vector3D(1, 0, 0) : new Vector3D(-1, 0, 0);
                            else
                                normalPoint = Math.Abs(onSegmentPoints[1].Y - wallContour_Y.Max()) < Math.Abs(onSegmentPoints[1].Y - wallContour_Y.Min()) ? new Vector3D(0, 1, 0) : new Vector3D(0, -1, 0);
                            tempWallModel.Children.Add(new RectangleVisual3D { Origin = originPoint, Length = Math.Max(Math.Abs(onSegmentPoints[1].X - onSegmentPoints[0].X), Math.Abs(onSegmentPoints[1].Y - onSegmentPoints[0].Y)), Width = bimWall.Holes[hole_id].Depth + holeModelOffset, Normal = normalPoint, Material = sidePlaneMaterial, BackMaterial = sidePlaneMaterial });
                        }

                    }
                    
                }
                else
                    throw new Exception("Json file are wrong in holes data, please recheck it!");

            }
            wallHolesModel.Children.Add(tempWallModel);
            wallModel = new ModelVisual3D();
            wallModel = wallHolesModel;
            return true;
        }
    }
}
