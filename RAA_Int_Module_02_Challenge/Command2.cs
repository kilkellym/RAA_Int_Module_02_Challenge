#region Namespaces
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

#endregion

namespace RAA_Int_Module_02_Challenge
{
    [Transaction(TransactionMode.Manual)]
    public class Command2 : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // this is a variable for the Revit application
            UIApplication uiapp = commandData.Application;

            // this is a variable for the current Revit model
            Document doc = uiapp.ActiveUIDocument.Document;

            List<View> collector = GetAllViews(doc);
            
            // Old code that didn't filter out view templates
            //FilteredElementCollector collector = new FilteredElementCollector(doc);
            //collector.OfCategory(BuiltInCategory.OST_Views);
            //collector.WhereElementIsNotElementType();

            int counter = 0;
            int viewCounter = 0;

            foreach (View curView in collector)
            {
                if (curView.IsTemplate == true)
                    continue;

                // 1. get the current view and its type
                ViewType curViewType = curView.ViewType;

                // 2. get the categories for the view type
                List<BuiltInCategory> catList = new List<BuiltInCategory>();
                Dictionary<ViewType, List<BuiltInCategory>> viewTypeCatDictionary = GetViewTypeCatDictionary();

                if (viewTypeCatDictionary.TryGetValue(curViewType, out catList) == false)
                {
                    continue;
                }

                // 3. get elements to tag for the view type
                ElementMulticategoryFilter catFilter = new ElementMulticategoryFilter(catList);
                FilteredElementCollector elemCollector = new FilteredElementCollector(doc, curView.Id);
                elemCollector.WherePasses(catFilter).WhereElementIsNotElementType();

                //TaskDialog.Show("test", $"Found {elemCollector.GetElementCount()} element");

                // 6. get dictionary of tag family symbols
                Dictionary<string, FamilySymbol> tagDictionary = GetTagDictionary(doc);

                // 4. loop through elements and tag
                

                using (Transaction t = new Transaction(doc))
                {
                    t.Start("Tag elements");
                    foreach (Element curElem in elemCollector)
                    {
                        bool addLeader = false;

                        if (curElem.Location == null)
                            continue;

                        // 5. get insertion point based on element type
                        XYZ point = GetInsertPoint(curElem.Location);

                        if (point == null)
                            continue;

                        // 7. get element data
                        string catName = curElem.Category.Name;

                        // 10. check cat name for walls
                        if (catName == "Walls")
                        {
                            addLeader = true;

                            if (IsCurtainWall(curElem))
                                catName = "Curtain Walls";
                        }

                        // 8. get tag based on element type
                        FamilySymbol elemTag = tagDictionary[catName];

                        // 9. tag element
                        if (catName == "Areas")
                        {
                            ViewPlan curAreaPlan = curView as ViewPlan;
                            Area curArea = curElem as Area;

                            AreaTag newTag = doc.Create.NewAreaTag(curAreaPlan, curArea, new UV(point.X, point.Y));
                            newTag.TagHeadPosition = new XYZ(point.X, point.Y, 0);
                        }
                        else
                        {
                            IndependentTag newTag = IndependentTag.Create(doc, elemTag.Id, curView.Id,
                                new Reference(curElem), addLeader, TagOrientation.Horizontal, point);

                            // 9a. offset tags as needed
                            if (catName == "Windows")
                                newTag.TagHeadPosition = point.Add(new XYZ(0, 3, 0));

                            if (curView.ViewType == ViewType.Section)
                                newTag.TagHeadPosition = point.Add(new XYZ(0, 0, 3));
                        }

                        counter++;
                    }
                    t.Commit();
                }
                viewCounter++;
            }


            TaskDialog.Show("Complete", $"Added {counter} tags to {viewCounter} views.");


            return Result.Succeeded;
        }

        public List<View> GetAllViews(Document curDoc)
        {
        	// gets all views and excludes any view templates
        	List<View> returnList = new List<View>();
        
        	FilteredElementCollector viewCollector = new FilteredElementCollector(curDoc);
        	viewCollector.OfCategory(BuiltInCategory.OST_Views);
        	viewCollector.WhereElementIsNotElementType();
        	
        	foreach (View view in viewCollector)
        	{
        		if (view.IsTemplate == false)
        			returnList.Add(view);
        	}
        
        	return returnList;
        }

        private bool IsCurtainWall(Element curElem)
        {
            Wall curWall = curElem as Wall;

            if (curWall.WallType.Kind == WallKind.Curtain)
                return true;

            return false;
        }

        private Dictionary<string, FamilySymbol> GetTagDictionary(Document doc)
        {
            Dictionary<string, FamilySymbol> catTagDict = new Dictionary<string, FamilySymbol>();

            catTagDict.Add("Rooms", GetTagByName(doc, "M_Room Tag"));
            catTagDict.Add("Doors", GetTagByName(doc, "M_Door Tag"));
            catTagDict.Add("Windows", GetTagByName(doc, "M_Window Tag"));
            catTagDict.Add("Furniture", GetTagByName(doc, "M_Furniture Tag"));
            catTagDict.Add("Lighting Fixtures", GetTagByName(doc, "M_Lighting Fixture Tag"));
            catTagDict.Add("Walls", GetTagByName(doc, "M_Wall Tag"));
            catTagDict.Add("Curtain Walls", GetTagByName(doc, "M_Curtain Wall Tag"));
            catTagDict.Add("Areas", GetTagByName(doc, "M_Area Tag"));
            return catTagDict;
        }

        private FamilySymbol GetTagByName(Document doc, string tagName)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .Where(x => x.FamilyName.Equals(tagName))
                .First();
        }

        private XYZ GetInsertPoint(Location loc)
        {
            LocationPoint locPoint = loc as LocationPoint;
            XYZ point;

            if (locPoint != null)
            {
                point = locPoint.Point;
            }
            else
            {
                LocationCurve locCurve = loc as LocationCurve;
                point = MidpointBetweenTwoPoints(locCurve.Curve.GetEndPoint(0), locCurve.Curve.GetEndPoint(1));

            }

            return point;
        }

        private XYZ MidpointBetweenTwoPoints(XYZ point1, XYZ point2)
        {
            XYZ midPoint = new XYZ((point1.X + point2.X) / 2, (point1.Y + point2.Y) / 2, (point1.Z + point2.Z) / 2);
            return midPoint;
        }

        private Dictionary<ViewType, List<BuiltInCategory>> GetViewTypeCatDictionary()
        {
            Dictionary<ViewType, List<BuiltInCategory>> dictionary = new Dictionary<ViewType, List<BuiltInCategory>>();

            dictionary.Add(ViewType.FloorPlan, new List<BuiltInCategory>
            {
                BuiltInCategory.OST_Rooms,
                BuiltInCategory.OST_Windows,
                BuiltInCategory.OST_Doors,
                BuiltInCategory.OST_Furniture,
                BuiltInCategory.OST_Walls
            });

            dictionary.Add(ViewType.AreaPlan, new List<BuiltInCategory> { BuiltInCategory.OST_Areas });

            dictionary.Add(ViewType.CeilingPlan, new List<BuiltInCategory>
            {
                BuiltInCategory.OST_Rooms,
                BuiltInCategory.OST_LightingFixtures
            });

            dictionary.Add(ViewType.Section, new List<BuiltInCategory> { BuiltInCategory.OST_Rooms });

            return dictionary;
        }
        internal static PushButtonData GetButtonData()
        {
            // use this method to define the properties for this command in the Revit ribbon
            string buttonInternalName = "btnCommand2";
            string buttonTitle = "Button 2";

            ButtonDataClass myButtonData1 = new ButtonDataClass(
                buttonInternalName,
                buttonTitle,
                MethodBase.GetCurrentMethod().DeclaringType?.FullName,
                Properties.Resources.Blue_32,
                Properties.Resources.Blue_16,
                "This is a tooltip for Button 2");

            return myButtonData1.Data;
        }
    }
}
