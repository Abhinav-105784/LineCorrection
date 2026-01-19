using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace LineCorrection
{
    internal class GetLineLayers
    {
        public GetLineLayers() { }
        public static async Task<List<FeatureLayer>> GetAllLinelayers() // Async = work method in background and not immediately without hanging arcgis and return the task of type list<featurelayer>
        {
            return await QueuedTask.Run(() => // run the method in it's(Arcgis) approved thread and await will prevent hanging of the software and queuedtask will run task in approved tasks thread of arcgis
            {
                var map = MapView.Active?.Map; //open the current map and ? means if it is null return null.
                if (map == null) return new List<FeatureLayer>(); // incase of null map return empty list
                return map.GetLayersAsFlattenedList().OfType<FeatureLayer>().Where(fl => fl.ShapeType == ArcGIS.Core.CIM.esriGeometryType.esriGeometryPolyline).ToList(); // get all the layers as flattened list that means ignore groups and then of type using lambda equation the feature layer where shapetype is polyline to a list.
            });
        }
    }
}
