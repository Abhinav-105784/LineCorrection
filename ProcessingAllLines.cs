using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.DDL;
using ArcGIS.Core.Geometry;
using ArcGIS.Core.Internal.CIM;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;
using Polyline = ArcGIS.Core.Geometry.Polyline;

namespace LineCorrection
{
    internal class ProcessingAllLines
    {
        public ProcessingAllLines() { }

        public static async Task<FeatureLayer> ProcessLayer(FeatureLayer layer, double angleThreshold)
        {
            var polylines = new List<ArcGIS.Core.Geometry.Polyline>();
            using (Table table = layer.GetTable())
            {
                using (var cursor = table.Search(null, false))
                {
                    while (cursor.MoveNext())
                    {
                        using (Row row = cursor.Current)
                        {
                            var shape = row["SHAPE"] as ArcGIS.Core.Geometry.Polyline;
                            if (shape != null)
                            {
                                polylines.Add(shape);
                            }
                        }
                    }
                }
            }
            if (polylines.Count == 0)
            {
                MessageBox.Show("No Polylines");
                return null;
            }

            var geometries = polylines.Cast<ArcGIS.Core.Geometry.Geometry>().ToList();
            var mergedGeometries = GeometryEngine.Instance.Union(geometries);
            var merged = mergedGeometries as ArcGIS.Core.Geometry.Polyline;

            if (merged == null)
            {
                MessageBox.Show("No Lines to Merge");
                return null;
            }

            List<MapPoint> hasAngleLessthanThreshold = AnglelessThanThreshHold(merged, angleThreshold);

            var buffers = new List<ArcGIS.Core.Geometry.Geometry>();
            foreach (var point in hasAngleLessthanThreshold)
            {
                var buffer = GeometryEngine.Instance.Buffer(point, 1.5);
                if (buffer != null) buffers.Add(buffer);
            }
            MessageBox.Show($"Total buffers created {buffers.Count}");

            if (buffers.Count == 0)
            {
                MessageBox.Show("No Buffers created");
                return null;
            }

            var unionBuffer = GeometryEngine.Instance.Union(buffers);
            if (unionBuffer == null || unionBuffer.IsEmpty)
            {
                MessageBox.Show("Nothing to union in buffer");
                return null;
            }

            var explodedLines = GeometryEngine.Instance.MultipartToSinglePart(merged).OfType<ArcGIS.Core.Geometry.Polyline>().ToList();

            MessageBox.Show($"Total line segments broken {explodedLines.Count}");

            var cleaned = new List<ArcGIS.Core.Geometry.Polyline>();
            foreach (var line in explodedLines)
            {
                var difference = GeometryEngine.Instance.Difference(line, unionBuffer) as ArcGIS.Core.Geometry.Polyline;
                if (difference != null && !difference.IsEmpty)
                {
                    cleaned.Add(difference);
                }

            }
            MessageBox.Show($"Final lines count {cleaned.Count}");

            if (cleaned.Count == 0)
            {
                MessageBox.Show("No Cleaned lines created");
                return null;
            }

            var cleanedLayer = await CreateNewCleanedLayer(layer);

            if (cleanedLayer == null)
            {
                MessageBox.Show("Failed to create Cleaned layer");
                return null;
            }

            await DrawCleanedPolylines(cleanedLayer, cleaned);

            return cleanedLayer;
        }

        // CHECKING IF ANGLE IS LESS THAN THRESHOLD GIVEN BY USER
        public static List<MapPoint> AnglelessThanThreshHold(ArcGIS.Core.Geometry.Polyline polyline, double threshold)
        {
            List<MapPoint> answer = new List<MapPoint>();

            var points = polyline.Points.ToList();

            if (points.Count < 3)
            {
                MessageBox.Show("Less points");
                return answer;
            }

            for (int i = 1; i < points.Count - 1; i++)
            {
                var p1 = points[i - 1];
                var p2 = points[i];
                var p3 = points[i + 1];

                double angle = CalculateAngle(p1, p2, p3);

                if (angle <= threshold) answer.Add(p2);
            }
            return answer;
        }

        //METHOD CALCULATING THE ANGLE BETWEEN VERTICES
        public static double CalculateAngle(MapPoint point1, MapPoint point2, MapPoint point3)
        {
            // vertex point1point2
            double v1x = point1.X - point2.X;
            double v1y = point1.Y - point2.Y;

            // vertex point3point2
            double v2x = point3.X - point2.X;
            double v2y = point3.Y - point2.Y;

            double dot = (v1x * v2x) + (v1y * v2y);
            double mag1 = Math.Sqrt(v1x * v1x + v1y * v1y);
            double mag2 = Math.Sqrt(v2x * v2x + v2y * v2y);

            double cosTheta = dot / (mag1 * mag2);

            //safety clamp

            cosTheta = Math.Max(-1, Math.Min(1, cosTheta));
            double angle = Math.Acos(cosTheta);
            return angle * (180 / Math.PI);
        }

        private static FeatureLayer GetFeatureLayer(Map map, string layerName)
        {
            return map.GetLayersAsFlattenedList().OfType<FeatureLayer>().FirstOrDefault(l => l.Name.Equals(layerName, StringComparison.OrdinalIgnoreCase));
        }


        private static async Task<FeatureLayer> CreateNewCleanedLayer(
    FeatureLayer sourceLayer)
        {
            string gdbPath = CoreModule.CurrentProject.DefaultGeodatabasePath;
            var map = MapView.Active.Map;

            string layerName = await GetNextCleanedLayerName();
            string fcPath = System.IO.Path.Combine(gdbPath, layerName);

            var sr = await QueuedTask.Run(() =>
                sourceLayer.GetFeatureClass()
                    .GetDefinition()
                    .GetSpatialReference());

            var args = Geoprocessing.MakeValueArray(
                gdbPath,
                layerName,
                "POLYLINE",
                "",
                "DISABLED",
                "DISABLED",
                sr);

            var result = await Geoprocessing.ExecuteToolAsync(
                "CreateFeatureclass_management", args);

            if (result.ErrorCode != 0)
                return null;

            // ❗ DO NOT manually add layer here

            // wait a tick so map refreshes
            await QueuedTask.Run(() => { });

            return map.GetLayersAsFlattenedList()
                .OfType<FeatureLayer>()
                .FirstOrDefault(l =>
                    l.Name.Equals(layerName,
                        StringComparison.OrdinalIgnoreCase));
        }


        private static async Task<string> GetNextCleanedLayerName()
        {
            string baseName = "Cleaned_Lines";
            string gdbPath = CoreModule.CurrentProject.DefaultGeodatabasePath;

            return await QueuedTask.Run(() =>
            {
                using (var gdb = new Geodatabase(
                    new FileGeodatabaseConnectionPath(
                        new Uri(gdbPath))))
                {
                    var names = gdb.GetDefinitions<FeatureClassDefinition>()
                        .Select(d => d.GetName())
                        .Where(n => n.StartsWith(baseName))
                        .ToList();

                    int max = 0;

                    foreach (var n in names)
                    {
                        if (n.Contains("_") &&
                            int.TryParse(
                                n.Split('_').Last(), out int i))
                            max = Math.Max(max, i);
                    }

                    return $"{baseName}_{max + 1}";
                }
            });
        }

        private static Task<bool> DrawCleanedPolylines(
            FeatureLayer targetLayer,
            List<Polyline> polylines)
        {
            return QueuedTask.Run(() =>
            {
                var fc = targetLayer.GetTable() as FeatureClass;
                var sr = fc.GetDefinition().GetSpatialReference();

                var editOp = new EditOperation
                {
                    Name = "Insert Cleaned Lines",
                    SelectNewFeatures = false
                };

                foreach (var pl in polylines)
                {
                    var geom = pl.SpatialReference.IsEqual(sr)
                        ? pl
                        : GeometryEngine.Instance.Project(pl, sr) as Polyline;

                    editOp.Create(targetLayer, geom);
                }

                return editOp.Execute();
            });
        }

    }
}
