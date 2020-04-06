using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;

// In order to load the result of this wizard, you will also need to
// add the output bin/ folder of this project to the list of loaded
// folder in Grasshopper.
// You can use the _GrasshopperDeveloperSettings Rhino command for that.

namespace FacadeTools
{
    public class FacadeToolsComponent : GH_Component
    {
        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public FacadeToolsComponent()
          : base("FacadeTools", "FacadeMesh",
              "Description",
              "Bowerbird", "Facade")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {

            // Use the pManager object to register your input parameters.
            // You can often supply default values when creating parameters.
            // All parameters must have the correct access type. If you want 
            // to import lists or trees of values, modify the ParamAccess flag.
            pManager.AddBrepParameter("Building Masses", "BM", "Building masses for facade generation", GH_ParamAccess.list);
            pManager.AddNumberParameter("Level Height", "LH", "Desired levels of floors given a brep", GH_ParamAccess.item, 4.0);
            pManager.AddNumberParameter("Panel Width", "PW", "Desired width of panel", GH_ParamAccess.item, 1.0);


            // If you want to change properties of certain parameters, 
            // you can use the pManager instance to access them by index:
            //pManager[0].Optional = true;

        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            // Use the pManager object to register your output parameters.
            // Output parameters do not have default values, but they too must have the correct access type.
            
            pManager.AddBrepParameter("Floor Surfaces", "FS", "Brep surfaces for each floor level", GH_ParamAccess.list);
            pManager.AddCurveParameter("Floor Edges", "FE", "Curve representation of floor edges", GH_ParamAccess.list);
            pManager.AddPointParameter("Edge Points", "EP", "Point representation of subdivided edges given a panal width", GH_ParamAccess.list);
            // Sometimes you want to hide a specific parameter from the Rhino preview.// You can use the HideParameter() method as a quick way://pManager.HideParameter(0);
        }
        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // First, we need to retrieve all data from the input parameters.
            // We'll start by declaring variables and assigning them starting values.
            List<Brep> breps = new List<Brep>();
            double levelHeight = 0.0;
            double PanelWidth = 0.0;

            // Then we need to access the input parameters individually. 
            // When data cannot be extracted from a parameter, we should abort this method.
            if (!DA.GetDataList(0, breps)) return;
            if (!DA.GetData(1, ref levelHeight)) return;
            if (!DA.GetData(2, ref PanelWidth)) return;

            // We should now validate the data and warn the user if invalid data is supplied.
            if (breps.Count < 1)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Number of breps need to exceed 1");
                return;
            }
            if (levelHeight < 0.0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Level height needs to be bigger than 0.0");
                return;
            }
            if (PanelWidth < 0.0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Panel Width Should be larger than 0.0");
                return;
            }

            // We're set to create the Mesh Facade now. To keep the size of the SolveInstance() method small, 
            // The actual functionality will be in a different method:
            List<Brep> BrepFaces = ComputeFloorSurfaces(breps).Item2;
            List<Curve> curves = GetEdges(breps);
            List<Point3d> points = DisplayPoints(breps, PanelWidth);

            // Finally assign the spiral to the output parameter.
            DA.SetDataList(0, BrepFaces);
            DA.SetDataList(1, curves);
            DA.SetDataList(2, points);
        }

        //----------------------------------------------------------------------------------------------------
        //Here Come the Methods
       
        //Getting Floor Breps
        private Tuple< List<BrepFace>, List<Brep> > ComputeFloorSurfaces(List<Brep> ListOfBreps)
        {

            Console.Write("Getting Floor Surfaces");
            List<BrepFace> faces = new List<BrepFace>();
            List<Brep> trimmedSurfaces = new List<Brep>();

            for (int i = 0; i < ListOfBreps.Count; i++)
            {

                Brep brep = ListOfBreps[i];

                Rhino.Geometry.Collections.BrepFaceList Faces = brep.Faces;
                for (int j = 0; j < Faces.Count; j++)
                {
                    Rhino.Geometry.BrepFace Face = Faces[j];
                    Vector3d normal = Face.NormalAt(0.5, 0.5);
                    if (normal.Z == -1)
                    {
                        faces.Add(Face);
                        Surface surface = Face.UnderlyingSurface();
                        Brep new_brep = Rhino.Geometry.Brep.CopyTrimCurves(Face, surface, 1e-6);
                        trimmedSurfaces.Add(new_brep);
                    }
                }
            }
            return Tuple.Create(faces, trimmedSurfaces);
        }

        //Getting Brep Edges
        private List<Curve> ComputeFloorEdges(List<BrepFace> ListofFaces)
        {
            Console.Write("Getting Floor Edges");
            List<Curve> OuterEdges = new List<Curve>();
            int counter = 0;
            for (int i = 0; i < ListofFaces.Count; i++)
            {

                OuterEdges.AddRange(ListofFaces[i].DuplicateFace(true).DuplicateEdgeCurves());
                counter = OuterEdges.Count;
            }
            Console.Write("Edge Count = " + counter.ToString());
            return OuterEdges;
        }

        private List<Curve> GetEdges(List<Brep> ListOfBreps)
        {
            List<BrepFace> flrs = ComputeFloorSurfaces(ListOfBreps).Item1;
            List<Curve> edges = ComputeFloorEdges(flrs);

            return edges;
        }

        //Subdividing Individual Edges
        private Point3d[][] SubdivideEdges(List<Curve> ListOfCurves, double SegmentLength)
        {
            Console.Write("Subdividing Edges");
            int CurveCount = ListOfCurves.Count;

            Rhino.Geometry.Point3d[][] PointGroups = new Point3d[CurveCount][];

            for (int i = 0; i < ListOfCurves.Count; i++)
            {
                Curve curve = ListOfCurves[i];
                double crv_length = curve.GetLength();
                string s = string.Format("Curve length is {0:f3}. Segment length", crv_length);
                Console.Write(s);
                Rhino.Geometry.Point3d[] points;
                curve.DivideByLength(SegmentLength, true, out points);
                PointGroups[i] = points;
            }
            return PointGroups;
        }

        //Getting Subdivision Points

        private List<Point3d> DisplayPoints(List<Brep> ListOfBreps, double SegmentLength)
        {
            List<BrepFace> flrs = ComputeFloorSurfaces(ListOfBreps).Item1;
            List<Curve> edges = ComputeFloorEdges(flrs);
            Point3d[][] points = SubdivideEdges(edges, SegmentLength);

            List<Point3d> FlattenedPoints = new List<Point3d>();
            for (int i = 0; i < points.Length; i++)
            {
                Point3d[] point = points[i];
                for (int j = 0; j < point.Length; j++)
                {
                    Point3d pnt = point[j];
                    FlattenedPoints.Add(pnt);
                }
            }

            return FlattenedPoints;
        }


        /// <summary>
        /// Provides an Icon for every component that will be visible in the User Interface.
        /// Icons need to be 24x24 pixels.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                // You can add image files to your project resources and access them like this:
                //return Resources.IconForThisComponent;
                return null;
            }
        }

        /// <summary>
        /// Each component must have a unique Guid to identify it. 
        /// It is vital this Guid doesn't change otherwise old ghx files 
        /// that use the old ID will partially fail during loading.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("1a1d679b-7974-4bf9-aeb7-952a5b2d3383"); }
        }
    }
}
   
