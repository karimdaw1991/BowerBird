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
    static class Globals
    {
        // global sorted breps
        public static Brep[] SortedBreps;

        // global heights
        public static double[] SortedHeights;

        // global Mesh Dictionary
        public static Dictionary<int, WindowElement> WindowDictionary;
    }


    public class WindowElement
    {
        // saving attributes, the attributes of this class with hopefully be filled up once the method MakeMeshFaces is called
        public int[] WindowPointIds { get; set; }
        public int WindowId { get; set; }
        public int EdgeNumber { get; set; }
        public int FloorNumber { get; set; }
        public int HouseNumber { get; set; }

        public WindowElement InitiateWindow(int id)
        {
            Dictionary<int, WindowElement> dict = Globals.WindowDictionary;
            WindowElement windowElement = new WindowElement(); 
            windowElement = dict[id];
            return windowElement;
        }

    }



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
            pManager.AddNumberParameter("Panel Width", "PW", "Desired width of panel", GH_ParamAccess.item, 1.0);
            pManager.AddIntegerParameter("Window Id", "Window Id", "Provides the Id to indicate a window#s attributes", GH_ParamAccess.item, 1);


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

            
            pManager.AddMeshParameter("Mesh Facade", "FM", "Mesh representation of facade", GH_ParamAccess.item);
            pManager.AddBrepParameter("Floor Surfaces", "FS", "Brep surfaces for each floor level", GH_ParamAccess.list);
            pManager.AddPointParameter("Vertex Points", "VP", "Vertex points of mesh faces", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Window Element Floor Number", "W_FloorNumber", "floor number of window", GH_ParamAccess.item);
            //pManager.AddPointParameter("Edge Points", "EP", "Point representation of subdivided edges given a panal width", GH_ParamAccess.list);
            // Sometimes you want to hide a specific parameter from the Rhino preview.// You can use the HideParameter() method as a quick way://pManager.HideParameter(0);
        }
        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        /// 



        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // First, we need to retrieve all data from the input parameters.
            // We'll start by declaring variables and assigning them starting values.
            List<Brep> breps = new List<Brep>();

            double PanelWidth = 0.0;
            int WindowId = 0;

            List<Point3d> pnts = new List<Point3d>();
            // Then we need to access the input parameters individually. 
            // When data cannot be extracted from a parameter, we should abort this method.
            if (!DA.GetDataList(0, breps)) return;
            if (!DA.GetData(1, ref PanelWidth)) return;
            if (!DA.GetData(2, ref WindowId)) return;


            // We should now validate the data and warn the user if invalid data is supplied.
            if (breps.Count < 1)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Number of breps need to exceed 1");
                return;
            }
            if (PanelWidth < 0.0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Panel Width Should be larger than 0.0");
                return;
            }

            // We're set to create the Mesh Facade now. To keep the size of the SolveInstance() method small, 
            // The actual functionality will be in a different method:

            //Step 1 Sort breps
            Brep[] sBreps = SortBrepByFloor(breps);
            //Step 2 Compute Sorted Floor Heights
            double[] sHeights = ComputeFloorHeights(breps);
            //Step 3 Make mesh
            Mesh mesh = MakeMeshFaces(breps, PanelWidth, out pnts);
            //Step 4 Display BottomFaces
            List<Brep> bottomFaces = DisplayAllBottomSurfaces(breps);
            //Step 5 Save data onto each face

            // for now we will just test this by sending teh data for the floor level,
            // later we will instead send over the window object as a whole
            // then create another compnenet that reads that data and does something with it.
            WindowElement WindowInstance = new WindowElement();
            WindowElement window10 = WindowInstance.InitiateWindow(WindowId);
            int floorNumber = window10.FloorNumber;




            // Finally assign the spiral to the output parameter.
            DA.SetData(0, mesh);
            DA.SetDataList(1, bottomFaces);
            DA.SetDataList(2, pnts);
            DA.SetData(3,floorNumber);
        }

        //----------------------------------------------------------------------------------------------------
        //Here Come the Methods


        // Sort Breps according to floor level
        private Brep[] SortBrepByFloor(List<Brep> ListOfBreps)
        {
            Console.Write("Sorting Breps");

            Brep[] breps = new Brep[ListOfBreps.Count];
            double[] zVals = new double[ListOfBreps.Count];

            for (int i = 0; i < ListOfBreps.Count; i++)
            {
                Brep brep = ListOfBreps[i];
                BoundingBox bbox = brep.GetBoundingBox(true);
                double zCom = bbox.Center.Z;
                int id = i;
                zVals[i] = zCom;
                breps[i] = ListOfBreps[i];
            }
            Array.Sort(zVals, breps);
            Globals.SortedBreps = breps;
            return breps;
        }

        // getting floor Heights
        private double[] ComputeFloorHeights(List<Brep> ListOfBreps)
        {

            Console.Write("Getting Floor Heights");
            Brep[] SortedBreps = Globals.SortedBreps;
            double[] heights = new double[SortedBreps.Length];

            for (int i = 0; i < SortedBreps.Length; i++)
            {
                Brep brep = SortedBreps[i];
                BoundingBox bbox = brep.GetBoundingBox(true);
                Vector3d vec = bbox.Diagonal;
                double height = vec.Z;
                heights[i] = height;
            }
            Globals.SortedHeights = heights;
            return heights;
        }


        //Getting Floor Breps
        private Tuple<BrepFace, Brep> ComputeFloorSurfaces(Brep brep)
        {
            List<BrepFace> faces = new List<BrepFace>();
            List<Brep> trimmedSurfaces = new List<Brep>();
            Rhino.Geometry.Collections.BrepFaceList Faces = brep.Faces;

            for (int i = 0; i < Faces.Count; i++)
            {
                Rhino.Geometry.BrepFace Face = Faces[i];
                Vector3d normal = Face.NormalAt(0.5, 0.5);
                if (normal.Z == -1)
                {
                    faces.Add(Face);
                    Surface surface = Face.UnderlyingSurface();
                    Brep new_brep = Rhino.Geometry.Brep.CopyTrimCurves(Face, surface, 1e-6);
                    trimmedSurfaces.Add(new_brep);

                }
            }
            BrepFace BottomFace = faces[0];
            Brep BottomFaceTrimmed = trimmedSurfaces[0];
            return Tuple.Create(BottomFace, BottomFaceTrimmed);
        }

        //Getting and Display all floor breps
        private List<Brep> DisplayAllBottomSurfaces(List<Brep> ListOfBreps)
        {
            List<Brep> trimmedFaces = new List<Brep>();
        
            for( int i=0; i< ListOfBreps.Count; i++ )
            {
                Brep brep = ListOfBreps[i];
                Tuple<BrepFace, Brep> tuple = ComputeFloorSurfaces(brep);
                Brep trimmedFace = tuple.Item2;
                trimmedFaces.Add(trimmedFace);
            }
            return trimmedFaces;
        }



        //Computing Floor Edges
        private List<Curve> ComputeFloorEdges(BrepFace face)
        {
            List<Curve> OuterEdges = new List<Curve>();
            int counter = 0;

            OuterEdges.AddRange(face.DuplicateFace(true).DuplicateEdgeCurves());
            counter = OuterEdges.Count;
            
            Console.Write("Edge Count = " + counter.ToString());
            return OuterEdges;
        }


        //Subdividing Individual Edges
        private Mesh MakeMeshFaces(List<Brep> ListOfBreps, double SegmentLength, out List<Point3d> pnts)
        {
            // initiate Dictionary to save windowElement attributes
            var Windows = new Dictionary<int, WindowElement>();


            //getting list of heights
            double[] heightsList = Globals.SortedHeights;
            Brep[] SortedBreps = Globals.SortedBreps;

            //Create 3d array of all PanalPointGroups
            Point3d[][][] AllPanalPointsGroup = new Point3d[SortedBreps.Length][][];

            List<Point3d> points = new List<Point3d>();

            Mesh mesh = new Mesh();
            // looping through srted Breps
            int pnt_Id = 0;
            int face_Id = 0;
            int edge_Id = 0;
            int floor_Id = 0;
            int mass_Id = 0;


            for (int i = 0; i < SortedBreps.Length; i++)
            {

                Brep brep = SortedBreps[i];
                //Getting Bottom Face of Brep
                BrepFace face = ComputeFloorSurfaces(brep).Item1;
                //Getting Edge curves of Face
                List<Curve> edges = ComputeFloorEdges(face);
                
                for (int j = 0; j < edges.Count; j++)
                {
                    Curve curve = edges[j];
                    double crv_length = curve.GetLength();
                    double[] curvePars = curve.DivideByLength(SegmentLength, true);
                    Curve[] splitSegmenets = curve.Split(curvePars);
                    int count = splitSegmenets.Length;
                    Point3d[][] panalPntGroups = new Point3d[count][];
                    for (int k = 0; k < count; k++)
                    {
                        double height = heightsList[i];
                        Curve seg = splitSegmenets[k];
                        Point3d pnt0 = seg.PointAtStart;
                        Point3d pnt1 = seg.PointAtEnd;
                        Point3d pnt2 = new Point3d(pnt1.X, pnt1.Y, pnt1.Z + height); // using global variable height
                        Point3d pnt3 = new Point3d(pnt0.X, pnt0.Y, pnt0.Z + height); // using global variable height
                        Point3d[] panalPnts = new Point3d[4];
                        int[] vertexIds = new int[4];

                        panalPnts[0] = pnt0;
                        panalPnts[1] = pnt1;
                        panalPnts[2] = pnt2;
                        panalPnts[3] = pnt2;

                        points.Add(pnt0);
                        points.Add(pnt1);
                        points.Add(pnt2);
                        points.Add(pnt3);

                        //AddingPoints to Mesh
                        mesh.Vertices.Add(pnt0);
                        mesh.Vertices.Add(pnt1);
                        mesh.Vertices.Add(pnt2);
                        mesh.Vertices.Add(pnt3);

                        int vertexId0 = pnt_Id;
                        int vertexId1 = pnt_Id + 1;
                        int vertexId2 = pnt_Id + 2;
                        int vertexId3 = pnt_Id + 3;

                        vertexIds[0] = vertexId0;
                        vertexIds[1] = vertexId1;
                        vertexIds[2] = vertexId2;
                        vertexIds[3] = vertexId3;

                        mesh.Faces.AddFace(vertexId0, vertexId1, vertexId2, vertexId3);
                        // Adding attributes to WindowElementObject
                        Windows.Add(face_Id,
                                new WindowElement
                                {
                                    WindowPointIds = vertexIds,
                                    WindowId = face_Id,
                                    EdgeNumber = edge_Id,
                                    FloorNumber = floor_Id,
                                    HouseNumber = mass_Id
                                }
                                );
                        pnt_Id = pnt_Id + 4;
                        face_Id = face_Id + 1;
                    }
                    edge_Id = edge_Id + 1;
                }
                floor_Id = floor_Id + 1;
            }
            mass_Id = mass_Id + 1;

            mesh.Normals.ComputeNormals();
            pnts = points;
            Globals.WindowDictionary = Windows;
            return mesh;
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
   
