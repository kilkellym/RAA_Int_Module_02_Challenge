﻿using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAA_Int_Module_02_Challenge
{
    internal class CommandAvailability : IExternalCommandAvailability
    {
        public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories)
        {
            bool result = false;
            UIDocument activeDoc = applicationData.ActiveUIDocument;
            if (activeDoc != null && activeDoc.Document != null)
            {
                result = true;
            }

            return result;
        }
    }
}
