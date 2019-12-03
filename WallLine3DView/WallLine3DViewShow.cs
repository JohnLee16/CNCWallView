using System;
using System.Collections.Generic;
using System.Text;
using HelixToolkit;
using HelixToolkit.Wpf;
using System.Windows.Media.Media3D;
using System.Windows.Media;
using System.Windows;

namespace WallLine3DView
{
    public class WallLine3DViewShow
    {
        public WallLine3DViewShow()
        {
            var modelGroup = new Model3DGroup();
            ReadJSONFile readJsonFile = new ReadJSONFile();
            
            
            // Create a mesh builder and add a box to it
            var meshBuilder = new MeshBuilder(false, false);
            //meshBuilder.AddBox(new Point3D(0, 0, 0), 1, 2, 0.5);
            meshBuilder.AddBox(new Rect3D(0, 0, 0, 2, 2, 2));
            List<Point> contours = new List<Point>() { new Point(0,0), new Point(0,800), new Point(58,800), new Point(58, 700), new Point(1592, 700), new Point(1592, 0), new Point(0, 0),
            };//new Point3D(0,0,112), new Point3D(0,800,112), new Point3D(58,800,112), new Point3D(58, 700, 112), new Point3D(1592, 700, 112), new Point3D(1592, 0, 112)
            
            PointCollection contour = new PointCollection() { new Point(0, 0), new Point(0, 800), new Point(58, 800), new Point(58, 700), new Point(1592, 700), new Point(1592, 0), new Point(0, 0) };
            Point3DCollection topSurface = new Point3DCollection() { new Point3D(0, 0, 0), new Point3D(0, 800, 0), new Point3D(58, 800, 0), new Point3D(58, 700, 0), new Point3D(1592, 700, 0), new Point3D(1592, 0, 0), new Point3D(0, 0, 0) };
            var axis = new Vector3D(0, 0, 12);
            var render = new ExtrudedVisual3D();
            meshBuilder.AddPolygon(topSurface);

            render.Section = contour;
            render.Path.Add(new Point3D(0, 0, 0));
            render.Path.Add(new Point3D(0, 0, 112));
            render.SectionXAxis = axis;
            render.Fill = Brushes.Green;
            render.IsPathClosed = true;
            render.IsSectionClosed = true;

            //meshBuilder.
            // Create a mesh from the builder (and freeze it)
            var mesh = meshBuilder.ToMesh(true);
            
            // Create some materials
            var greenMaterial = MaterialHelper.CreateMaterial(Colors.Green);
            var redMaterial = MaterialHelper.CreateMaterial(Colors.Red);
            var blueMaterial = MaterialHelper.CreateMaterial(Colors.Blue);
            var insideMaterial = MaterialHelper.CreateMaterial(Colors.Yellow);

            // Add 3 models to the group (using the same mesh, that's why we had to freeze it)
            modelGroup.Children.Add(new GeometryModel3D { Geometry = mesh, Material = greenMaterial, BackMaterial = insideMaterial });
            //modelGroup.Children.Add(new GeometryModel3D { Geometry = mesh, Transform = new TranslateTransform3D(-2, 0, 0), Material = redMaterial, BackMaterial = insideMaterial });
            //modelGroup.Children.Add(new GeometryModel3D { Geometry = mesh, Transform = new TranslateTransform3D(2, 0, 0), Material = blueMaterial, BackMaterial = insideMaterial });
            render.IsAttachedToViewport3D();
            // Set the property, which will be bound to the Content property of the ModelVisual3D (see MainWindow.xaml)
            //this.WallModel = modelGroup;
            //this.WallModel = modelGroup;
            
        }
        public Model3D WallModel { get; set; }
        public ExtrudedVisual3D ExtrudedWall { get; set; }
    }
}
