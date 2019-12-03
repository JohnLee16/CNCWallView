﻿using System;
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

            WallModelCreater(bimWall, out ModelVisual3D wallModel);

            RebarSlotModelCreater(bimWall, wallModel);
            HoleModelCreater(bimWall, wallModel);
            ConeModelCreater(bimWall, wallModel);
            RebarMountModelCreater(bimWall, wallModel);

            wallFinalModel.Children.Add(wallModel);
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
            var grayMaterial = MaterialHelper.CreateMaterial(Colors.WhiteSmoke);
            var insideMaterial = MaterialHelper.CreateMaterial(Colors.WhiteSmoke);

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
            float holeModelOffset = 0.01f;
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
            float rebarVisualOffset = 0.006f;
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
                            Length = MathF.Abs(bimWall.Rebars[rebar_id].StartPoint.X - bimWall.Rebars[rebar_id].EndPoint.X) + rebarVisualOffset,
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
                            Length = rebarWidth + rebarVisualOffset,
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
                    rebarMount = RebarMount(bimWall.RebarMounts[mount_id], Math.Min(bimWall.Thickness - bimWall.RebarMounts[mount_id].StartPoint.Y, bimWall.RebarMounts[mount_id].StartPoint.Y), Colors.Black, true);
                }
                else
                    rebarMount = RebarMount(bimWall.RebarMounts[mount_id], Math.Min(bimWall.Thickness - bimWall.RebarMounts[mount_id].StartPoint.Y, bimWall.RebarMounts[mount_id].StartPoint.Y), Colors.Black);
                
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
            double coneVisualOffset = 0.003;
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
        public ModelVisual3D RebarMount(BimRebarMount bimRebarMount, float rebarMountDepth, Color color)
        {
            float EPSILON = 0.001f;
            double rebarMountDiameter = 20 + 0.006;
            double rebarMountVisualOffset = 0.01;
            double rebarMountOffset = 15;
            double rebarMountDistance = 10;
            
            ModelVisual3D rebarMountVisual3D = new ModelVisual3D();
            
            if (bimRebarMount.Orientation == true)
            {
                Point3D boxCenter = Math.Abs(bimRebarMount.Direction.X - 1) < EPSILON ? new Point3D(bimRebarMount.StartPoint.X + rebarMountOffset / 2, bimRebarMount.StartPoint.Z, bimRebarMount.StartPoint.Y / 2) : new Point3D(bimRebarMount.StartPoint.X - rebarMountOffset / 2, bimRebarMount.StartPoint.Z, bimRebarMount.StartPoint.Y / 2);
                Point3D cylinderCenter1 = Math.Abs(bimRebarMount.Direction.X - 1) < EPSILON ? new Point3D(bimRebarMount.StartPoint.X + rebarMountOffset, bimRebarMount.StartPoint.Z, bimRebarMount.StartPoint.Y - rebarMountVisualOffset) : new Point3D(bimRebarMount.StartPoint.X - rebarMountOffset, bimRebarMount.StartPoint.Z, bimRebarMount.StartPoint.Y - rebarMountVisualOffset);
                Point3D cylinderCenter2 = Math.Abs(bimRebarMount.Direction.X - 1) < EPSILON ? new Point3D(bimRebarMount.StartPoint.X + rebarMountOffset, bimRebarMount.StartPoint.Z, bimRebarMount.StartPoint.Y - rebarMountDepth - rebarMountVisualOffset) : new Point3D(bimRebarMount.StartPoint.X - rebarMountOffset, bimRebarMount.StartPoint.Z, bimRebarMount.StartPoint.Y - rebarMountDepth - rebarMountVisualOffset);
                var rebarMountMeshBuilder = new MeshBuilder(false, false);
                rebarMountMeshBuilder.AddBox(boxCenter, rebarMountOffset, rebarMountDiameter, rebarMountDepth + rebarMountVisualOffset);//Point3D center, double xlength, double ylength, double zlength
                rebarMountMeshBuilder.AddCylinder(cylinderCenter1, cylinderCenter2, rebarMountDiameter / 2, 360, true, true);
                var rebarMountMesh = rebarMountMeshBuilder.ToMesh(true);

                var rebarMountMaterial = MaterialHelper.CreateMaterial(Colors.Black);
                Model3DGroup rebarMountModelGroup = new Model3DGroup();
                rebarMountModelGroup.Children.Add(new GeometryModel3D { Geometry = rebarMountMesh, Material = rebarMountMaterial, BackMaterial = rebarMountMaterial });

                rebarMountVisual3D.Content = rebarMountModelGroup;
            } 
            else if (bimRebarMount.Orientation == false)
            {
                Point3D boxCenter = Math.Abs(bimRebarMount.Direction.X - 1) < EPSILON ? new Point3D(bimRebarMount.StartPoint.X + rebarMountOffset / 2, bimRebarMount.StartPoint.Z, bimRebarMount.StartPoint.Y + rebarMountDepth / 2) : new Point3D(bimRebarMount.StartPoint.X - rebarMountOffset / 2, bimRebarMount.StartPoint.Z, bimRebarMount.StartPoint.Y + rebarMountDepth / 2);
                Point3D cylinderCenter1 = Math.Abs(bimRebarMount.Direction.X - 1) < EPSILON ? new Point3D(bimRebarMount.StartPoint.X + rebarMountOffset, bimRebarMount.StartPoint.Z, bimRebarMount.StartPoint.Y + rebarMountVisualOffset) : new Point3D(bimRebarMount.StartPoint.X - rebarMountOffset, bimRebarMount.StartPoint.Z, bimRebarMount.StartPoint.Y + rebarMountVisualOffset);
                Point3D cylinderCenter2 = Math.Abs(bimRebarMount.Direction.X - 1) < EPSILON ? new Point3D(bimRebarMount.StartPoint.X + rebarMountOffset, bimRebarMount.StartPoint.Z, bimRebarMount.StartPoint.Y + rebarMountDepth + rebarMountVisualOffset) : new Point3D(bimRebarMount.StartPoint.X - rebarMountOffset, bimRebarMount.StartPoint.Z, bimRebarMount.StartPoint.Y + rebarMountDepth + rebarMountVisualOffset);
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

        public ModelVisual3D RebarMount(BimRebarMount bimRebarMount, float rebarMountDepth, Color color, bool insideContour)
        {
            float EPSILON = 0.001f;
            double rebarMountDiameter = 20 + 0.006;
            double rebarMountVisualOffset = 0.01;
            double rebarMountOffset = 15;

            ModelVisual3D rebarMountVisual3D = new ModelVisual3D();
            if (bimRebarMount.Orientation == true)
            {
                Point3D cylinderCenter1 = Math.Abs(bimRebarMount.Direction.X - 1) < EPSILON ? new Point3D(bimRebarMount.StartPoint.X + rebarMountOffset, bimRebarMount.StartPoint.Z, bimRebarMount.StartPoint.Y - rebarMountVisualOffset) : new Point3D(bimRebarMount.StartPoint.X - rebarMountOffset, bimRebarMount.StartPoint.Z, bimRebarMount.StartPoint.Y - rebarMountVisualOffset);
                Point3D cylinderCenter2 = Math.Abs(bimRebarMount.Direction.X - 1) < EPSILON ? new Point3D(bimRebarMount.StartPoint.X + rebarMountOffset, bimRebarMount.StartPoint.Z, bimRebarMount.StartPoint.Y - rebarMountDepth - rebarMountVisualOffset) : new Point3D(bimRebarMount.StartPoint.X - rebarMountOffset, bimRebarMount.StartPoint.Z, bimRebarMount.StartPoint.Y - rebarMountDepth - rebarMountVisualOffset);
                var rebarMountMeshBuilder = new MeshBuilder(false, false);
                rebarMountMeshBuilder.AddCylinder(cylinderCenter1, cylinderCenter2, rebarMountDiameter / 2, 360, true, true);
                var rebarMountMesh = rebarMountMeshBuilder.ToMesh(true);

                var rebarMountMaterial = MaterialHelper.CreateMaterial(Colors.Black);
                Model3DGroup rebarMountModelGroup = new Model3DGroup();
                rebarMountModelGroup.Children.Add(new GeometryModel3D { Geometry = rebarMountMesh, Material = rebarMountMaterial, BackMaterial = rebarMountMaterial });

                rebarMountVisual3D.Content = rebarMountModelGroup;
            }
            else if (bimRebarMount.Orientation == false)
            {                
                Point3D cylinderCenter1 = Math.Abs(bimRebarMount.Direction.X - 1) < EPSILON ? new Point3D(bimRebarMount.StartPoint.X + rebarMountOffset, bimRebarMount.StartPoint.Z, bimRebarMount.StartPoint.Y + rebarMountVisualOffset) : new Point3D(bimRebarMount.StartPoint.X - rebarMountOffset, bimRebarMount.StartPoint.Z, bimRebarMount.StartPoint.Y + rebarMountVisualOffset);
                Point3D cylinderCenter2 = Math.Abs(bimRebarMount.Direction.X - 1) < EPSILON ? new Point3D(bimRebarMount.StartPoint.X + rebarMountOffset, bimRebarMount.StartPoint.Z, bimRebarMount.StartPoint.Y + rebarMountDepth + rebarMountVisualOffset) : new Point3D(bimRebarMount.StartPoint.X - rebarMountOffset, bimRebarMount.StartPoint.Z, bimRebarMount.StartPoint.Y + rebarMountDepth + rebarMountVisualOffset);
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
            double EPSLION = 5;
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
    }
}
