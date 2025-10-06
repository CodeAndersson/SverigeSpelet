using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using System.Threading.Tasks;
using System.Windows.Input;

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
            // Markera event som hanterat när vänster musknapp trycks ned
            if (e.ChangedButton == MouseButton.Left)
            {
                e.Handled = true;
            }
        }

        protected override Task HandleMouseDownAsync(MapViewMouseButtonEventArgs e)
        {
            return QueuedTask.Run(() =>
            {
                // Konvertera musposition till kartkoordinater
                var mapClickPoint = MapView.Active.ClientToMap(e.ClientPoint);

                // Skicka klicket till vår DockPane
                var dockpane = FrameworkApplication.DockPaneManager.Find("SverigeSpelet_SverigeSpeletDockpane") as SverigeSpeletDockpaneViewModel;
                dockpane?.HanteraKartKlick(mapClickPoint);
            });
        }
    }
}