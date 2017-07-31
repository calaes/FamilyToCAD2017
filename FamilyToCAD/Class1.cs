﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.Creation;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;

[TransactionAttribute(TransactionMode.Manual)]
[RegenerationAttribute(RegenerationOption.Manual)]
public class FamilyToCAD1 : IExternalCommand
{
    public Result Execute(
        ExternalCommandData commandData,
        ref string message,
        ElementSet elements)
    {
        UIApplication uiApp = commandData.Application;

        FamilyToCAD.Form2 form = new FamilyToCAD.Form2();
        form.ShowDialog();

        ///Sets up a document to place family into
        Autodesk.Revit.DB.Document uiDoc = null;

        if (form.ProjectLocation == "New Project")
        {
            try
            {
                uiDoc = uiApp.Application.NewProjectDocument(form.TemplateFile);
            }
            catch
            {
                return Result.Failed;
            }

            if (!Directory.Exists(@"C:\temp\"))
            {
                Directory.CreateDirectory(@"C:\temp\");
            }

            if (File.Exists(@"C:\temp\new_project.rvt"))
            {
                File.Delete(@"C:\temp\new_project.rvt");
            }

            SaveAsOptions options1 = new SaveAsOptions();
            options1.OverwriteExistingFile = true;
            uiDoc.SaveAs(@"C:/temp/new_project.rvt", options1);

            uiApp.OpenAndActivateDocument(@"C:/temp/new_project.rvt");
        }
        else if (form.ProjectLocation == "<Current Project>")
        {
            if (uiApp.ActiveUIDocument != null)
            {
                uiDoc = uiApp.ActiveUIDocument.Document;
            }
            else
            {
                MessageBox.Show("No Currently Loaded Project. Please Rerun addin and select a \"New Project\"");
                return Result.Failed;
            }
        }

        if (uiDoc == null)
        {
            return Result.Failed;
        }

        if (File.Exists(form.FamilyFile))
        {
            using (Transaction tx = new Transaction(uiDoc))
            {
                tx.Start("Load Family");
                Autodesk.Revit.DB.Family family = null;
                uiDoc.LoadFamily(form.FamilyFile, out family);
                tx.Commit();

                Double low = 0;

                var collector = new FilteredElementCollector(uiDoc);

                var viewFamilyType = collector
                    .OfClass(typeof(ViewFamilyType))
                    .OfType<ViewFamilyType>()
                    .FirstOrDefault(x => x.ViewFamily == ViewFamily.ThreeDimensional);

                // Export the active view
                ICollection<ElementId> views = new List<ElementId>();

                string name = family.Name;
                foreach (ElementId id in family.GetFamilySymbolIds())
                {
                    tx.Start("ChangeView");
                    View3D view = View3D.CreateIsometric(uiDoc, viewFamilyType.Id);
                    view.DisplayStyle = DisplayStyle.Shading;
                    tx.Commit();

                    FamilySymbol famsymbol = family.Document.GetElement(id) as FamilySymbol;
                    XYZ origin = new XYZ(0, low, 0);
                    tx.Start("Load Family Member");
                    famsymbol.Activate();
                    FamilyInstance instance = uiDoc.Create.NewFamilyInstance(origin, famsymbol, StructuralType.NonStructural);

                    //FamilyInstance instance1 = uiDoc.Create.NewFamilyInstance(origin,famsymbol,View.);
                    tx.Commit();

                    BoundingBoxXYZ BB = famsymbol.get_BoundingBox(view);

                    XYZ BBMin = BB.Min;

                    //XYZ trans = new XYZ(origin.X + BB.Min.X, origin.Y + BB.Min.Y, 0);
                    //tx.Start("Element Move");
                    //instance.Location.Move(trans);
                    //tx.Commit();

                    low = low + BBMin.Y;

                }

                tx.Start("ChangeOverallView");
                View3D overallview = View3D.CreateIsometric(uiDoc, viewFamilyType.Id);
                views.Add(overallview.Id);
                overallview.DisplayStyle = DisplayStyle.Shading;
                tx.Commit();

                DWGExportOptions options = new DWGExportOptions();
                options.FileVersion = ACADVersion.R2007;
                options.ExportOfSolids = SolidGeometry.ACIS;
                options.ACAPreference = ACAObjectPreference.Object;
                options.HideUnreferenceViewTags = true;

                //string dwgfilename = family.Name + ".dwg";
                //string dwgfullfilename = filedir + dwgfilename;
                //if (File.Exists(dwgfullfilename))
                //{
                //    File.Delete(dwgfullfilename);
                //}
                //uiDoc.Export(@filedir, @dwgfilename, views, options);

            }
        }
        else
        {
            Console.WriteLine("Please Create Export Directory For the chosen CAPS file.");
        }

        return Result.Succeeded;
    }
}