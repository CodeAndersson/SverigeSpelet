using System;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace SverigeSpelet
{
    internal class SverigeSpeletMapTool : MapTool
    {
        public SverigeSpeletMapTool()
        {
            IsSketchTool = false;
            SketchType = SketchGeometryType.Point;
            SketchOutputMode = SketchOutputMode.Map;
        }

        protected override void OnToolMouseDown(MapViewMouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                e.Handled = true;
                System.Diagnostics.Debug.WriteLine("MapTool: Vänsterklick fångat");
            }
        }

        protected override Task HandleMouseDownAsync(MapViewMouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                return QueuedTask.Run(() =>
                {
                    try
                    {
                        var mapClickPoint = MapView.Active.ClientToMap(e.ClientPoint);
                        System.Diagnostics.Debug.WriteLine($"Klickposition: {mapClickPoint.X:F2}, {mapClickPoint.Y:F2}");

                        var dockpane = FrameworkApplication.DockPaneManager.Find("SverigeSpelet_SverigeSpeletDockpane") as SverigeSpeletDockpaneViewModel;

                        if (dockpane != null)
                        {
                            System.Diagnostics.Debug.WriteLine("Skickar kartklick till DockPane");
                            dockpane.HanteraKartKlick(mapClickPoint);
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("DockPane inte hittad");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Fel i MapTool: {ex.Message}");
                    }
                });
            }
            return Task.CompletedTask;
        }

    }
}
