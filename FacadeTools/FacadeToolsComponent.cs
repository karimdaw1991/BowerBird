using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// In order to load the result of this wizard, you will also need to
// add the output bin/ folder of this project to the list of loaded
// folder in Grasshopper.
// You can use the _GrasshopperDeveloperSettings Rhino command for that.

namespace FacadeTools
{
    static class Globals
    {
        // global 
        public static List<MassObject> MassObjects;

        // global sorted breps
        public static Brep[] SortedBreps;

        // global heights
        public static double[] SortedHeights;

        // global Mesh Dictionary
        public static Dictionary<int, WindowElement> WindowDictionary;

        // global house Ids
        public static List<int> HouseIds;
    }

    public class BuildingObject
    {
       
        public int HouseNumber { get; set; }
        public int FloorCount { get; set; }
        public List<MassObject> MassObjects { get; set; }

        public BuildingObject()
        {
            HouseNumber = 0;
            FloorCount = 0;
            MassObjects = new List<MassObject>();
        }

        public BuildingObject(List<MassObject> ListofMassObjects)
        {
            MassObjects = ListofMassObjects;
        }
    }

    public class MassObject : BuildingObject
    {
        public int BuildingNumber { get; set; }
        public int MassId { get; set; }
        public int FloorNumber { get; set; }
        public double XComp { get; set; }
        public double YComp { get; set; }
        public double ZComp { get; set; }
        public double Height { get; set; }
        public double Area { get; set; }
        public Brep MassBrep { get; set; }
        public Point3d CenterPoint { get; set; }

        public MassObject()
        {
            MassId = 0;
            FloorNumber = 0;
            XComp = 0;
            YComp = 0;
            ZComp = 0;
            Height = 0;
            Area = 0;
            MassBrep = new Brep();
            CenterPoint = new Point3d();
        }

        public MassObject(Brep brep)
        {
            MassBrep = brep;
        }
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
            pManager.AddIntegerParameter("mass Id", "mass Id", "Provides the Id to indicate a mass attributes", GH_ParamAccess.item, 1);


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


            //pManager.AddMeshParameter("Mesh Facade", "FM", "Mesh representation of facade", GH_ParamAccess.item);
            //pManager.AddBrepParameter("Floor Surfaces", "FS", "Brep surfaces for each floor level", GH_ParamAccess.list);
            pManager.AddIntegerParameter("BuildingObject number", "Building Number", "Id of building object", GH_ParamAccess.item);
            pManager.AddIntegerParameter("MassObject number", "MassObject Number", "Id of mass object", GH_ParamAccess.item);
            pManager.AddNumberParameter("MassObject Height", "MassObject height", "height of mass object", GH_ParamAccess.item);
            pManager.AddIntegerParameter("HouseNumber", "HouseNumber", "Id of mass object", GH_ParamAccess.item);
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
            int massId = 0;

            List<Point3d> pnts = new List<Point3d>();
            // Then we need to access the input parameters individually. 
            // When data cannot be extracted from a parameter, we should abort this method.
            if (!DA.GetDataList(0, breps)) return;
            if (!DA.GetData(1, ref PanelWidth)) return;
            if (!DA.GetData(2, ref massId)) return;


            // We should now validate the data and warn the user if invalid data is supplied.
            if (breps.Count < 1)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Number of breps need to exceed 1");
                return;
            }
            if (PanelWidth < 0.5)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Panel Width Should be larger than 0.5");
                return;
            }

            // We're set to create the Mesh Facade now. To keep the size of the SolveInstance() method small, 
            // The actual functionality will be in a different method:

            //Step 1 Sort breps
            //Brep[] sBreps = SortBrepByFloor(breps);
            //Step 2 Compute Sorted Floor Heights
            //double[] sHeights = ComputeFloorHeights(breps);
            //Step 3 Compute Order of House Ids
            //ComputeHouseIds(breps);
            

            ConvertBrepListToMassObjects(breps);
            ClusterMassesIntoBuildings();
            MassObject massobject = Globals.MassObjects[massId];
            int MyBuildingId = massobject.BuildingNumber;
            int MyMassId = massobject.MassId;
            double MyMassHeight = massobject.Height;
            int MyMassHouseNumber = massobject.HouseNumber;



            //Step 3 Make mesh
            //Mesh mesh = MakeMeshFaces(breps, PanelWidth, out pnts);
            //Step 4 Display BottomFaces
            //List<Brep> bottomFaces = DisplayAllBottomSurfaces(breps);
            //Step 5 Save data onto each face

            // for now we will just test this by sending teh data for the floor level,
            // later we will instead send over the window object as a whole
            // then create another compnenet that reads that data and does something with it.
            /*
            WindowElement WindowInstance = new WindowElement();
            WindowElement window10 = WindowInstance.InitiateWindow(WindowId);
            int floorNumber = window10.FloorNumber;
            int windowNumber = window10.WindowId;
            int EdgeNumber = window10.EdgeNumber;
            int HouseNumber = window10.HouseNumber;
            */


            // Finally assign the spiral to the output parameter.
            //DA.SetData(0, mesh);
            //DA.SetDataList(1, bottomFaces);
            //DA.SetDataList(2, pnts);
            DA.SetData(0, MyBuildingId);
            DA.SetData(1, MyMassId);
            DA.SetData(2, MyMassHeight);
            DA.SetData(3, MyMassHouseNumber);
        }

        //----------------------------------------------------------------------------------------------------
        //Here Come the Methods
        private MassObject BrepToMassObject(Brep brep)
        {
            MassObject mass_object = new MassObject(brep);
            BoundingBox bbox = brep.GetBoundingBox(true);
            mass_object.CenterPoint = bbox.Center;
            mass_object.XComp = bbox.Center.X;
            mass_object.YComp = bbox.Center.Y;
            mass_object.ZComp = bbox.Center.Z;
            mass_object.Height = bbox.Diagonal.Z;
            mass_object.Area = ComputeFloorSurfaces(brep).Item2.GetArea();
            return mass_object;
        }

        // looping through all givien breps and converting all to MassObjects adn giving each of them IDs
        private void ConvertBrepListToMassObjects(List<Brep> breps)
        {
            List<MassObject> massObjects = new List<MassObject>();
            for(int i = 0; i < breps.Count;i++)
            {
                Brep brep = breps[i];
                MassObject mObject = BrepToMassObject(brep);
                // Giving all massObjects an id with the iterator
                mObject.MassId = i;
                massObjects.Add(mObject);
            }

            Globals.MassObjects = massObjects;
        }

        // Catagorizing breps into buildings
        // For now let us create something that gives us back an array that indicates the ID of the buildings

        private void ClusterMassesIntoBuildings()
        {
            double threshold = 10.0;
            List<MassObject> massObjects = Globals.MassObjects;
            List<Brep> newList = new List<Brep>();
            //Using Query Syntax
            var HousesGroupedByXandY = from massObject in massObjects
                                       group massObject by new
                                        {
                                           massObject.XComp,
                                           massObject.YComp,
                                        } into massGroup
                                        orderby massGroup.Key.XComp ascending,
                                                massGroup.Key.YComp ascending
                                        select new
                                        {
                                            x = massGroup.Key.XComp,
                                            y = massGroup.Key.YComp,
                                            Building = massGroup.OrderBy(z => z.ZComp)
                                        };
            int id = 0;
            int floorCount = 0;
            List<BuildingObject> buildingObjects = new List<BuildingObject>();
            foreach (var group in HousesGroupedByXandY)
            {
                BuildingObject buildingObject = new BuildingObject();
                buildingObject.HouseNumber = id;
                foreach (MassObject massObject in group.Building)
                {
                    massObject.FloorNumber = floorCount;
                    massObject.BuildingNumber = id;
                    // adding massobjects to list of mass objects in building object class
                    buildingObject.MassObjects.Add(massObject);
                    floorCount++;
                    buildingObjects.Add(buildingObject);
                }
                id++;
            }
           
        }
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

        /*
        private void ComputeHouseIds(List<Brep> ListOfBreps)
        {
            double threshold = 10.0;
            Brep[] SortedBreps = Globals.SortedBreps;
            List<Brep> newList = new List<Brep>();
            List<int> test = new List<int>();
            // here we see the test passed, the loop is adding the right amount of objects
            // question is why is it when we int a brep to put in new list, it only adds one object. 
            // we first establish if the problem is with the global variables
            List<int> listOfGroups = new List<int>();
            List<Brep> listOfBreps = new List<Brep>(SortedBreps);
            // i think we need to add the first id to the list
            int ID = 0;
            listOfGroups.Add(0);
            // First we need to find the center points 
            for (int i = 0; i < listOfBreps.Count; i++)
            {
                Brep brep = listOfBreps[i];
                BoundingBox bbox = brep.GetBoundingBox(true);
                Point3d centerPoint = bbox.Center;
                double x = centerPoint.X;
                double y = centerPoint.Y;
                for (int j = i + 1; j < listOfBreps.Count;)
                {
                    Brep brep2 = listOfBreps[j];
                    BoundingBox bbox2 = brep2.GetBoundingBox(true);
                    Point3d centerPoint2 = bbox2.Center;
                    double x2 = centerPoint2.X;
                    double y2 = centerPoint2.Y;

                    double vectorX = x - x2;
                    double vectorY = y - y2;


                    double length = Math.Sqrt(Math.Pow(vectorX, 2) + Math.Pow(vectorY, 2));

                    if (length <= threshold)
                    {
                        listOfGroups.Add(ID);
                        listOfBreps.RemoveAt(j);
                    }
                    else
                    {
                        ID = ID + 1;
                        listOfGroups.Add(ID);
                        j += 1;
                    }
                }
            }
            Globals.HouseIds = listOfGroups;
        }
        */

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
            List<int> ListOfHouseIds = Globals.HouseIds;

            //getting list of heights
            double[] heightsList = Globals.SortedHeights;
            Brep[] SortedBreps = Globals.SortedBreps;

            //Create 3d array of all PanalPointGroups
            Point3d[][][] AllPanalPointsGroup = new Point3d[SortedBreps.Length][][];

            List<Point3d> points = new List<Point3d>();

            Mesh mesh = new Mesh();
            // looping through srted Breps
            int pnt_Id = new int();
            int face_Id = new int();
            int edge_Id = new int();
            int floor_Id = new int();
            int mass_Id = new int();

            List<int> windowsPerFloors = new List<int>(); 
            for (int i = 0; i < SortedBreps.Length; i++)
            {

                Brep brep = SortedBreps[i];
                //Getting Bottom Face of Brep
                BrepFace face = ComputeFloorSurfaces(brep).Item1;
                //Getting Edge curves of Face
                List<Curve> edges = ComputeFloorEdges(face);
                // we need to know how many windows are on each floor
                int windowsPerFloor = 0;
                
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
                        windowsPerFloor = windowsPerFloor + 1;
                       
                    }

                    edge_Id = edge_Id + 1;
                    windowsPerFloors.Add(windowsPerFloor);
                }
                if (face_Id < windowsPerFloors[i])
                {
                    floor_Id = 0;
                }
                else
                {
                    floor_Id = floor_Id + 1;
                }
                // here we still need to fix some bugs so that the program knows a few things:
                // 1 - Whether teh window knows it is at the same address , meaning brep groups..
                // 2 - knowing to reset the floor count back to 0 if the address changes. 
                mass_Id = ListOfHouseIds[i];
            }
            
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
   
