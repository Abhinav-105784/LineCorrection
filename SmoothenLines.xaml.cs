using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;


namespace LineCorrection
{
    /// <summary>
    /// Interaction logic for SmoothenLinesView.xaml
    /// </summary>
    public partial class SmoothenLinesView : UserControl
    {
        public SmoothenLinesView()
        {
            InitializeComponent();
            AngleProvided.Text=string.Empty;
            this.Loaded += Smoothen_SteepAnglesView_Loaded;
        }
        private async void Smoothen_SteepAnglesView_Loaded(object sender, RoutedEventArgs e) // again async coz we await the method to produce list of line layers
        {
            var lineLayers = await GetLineLayers.GetAllLinelayers(); // await keyword to prevent hanging Arcgis
            SelectLayerComboBox.ItemsSource = lineLayers; // providing data to the combobox
            SelectLayerComboBox.DisplayMemberPath = "Name"; // to display the name property of every polyline feature layer.
        }
        private void SelectLineLayers(object sender, SelectionChangedEventArgs e)
        {

        }

        private void AngleProvided_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private async void RunButton_Click(object sender, RoutedEventArgs e)
        {

            if (SelectLayerComboBox.SelectedItem is not FeatureLayer layer)
            {
                MessageBox.Show("Please select a line layer"); return;
            }

            if (!double.TryParse(AngleProvided.Text, out double angle))
            {
                MessageBox.Show("Invalid angle Value");
                return;
            }

            // again running this process in background thread to prevent arcgis from hanging
            await QueuedTask.Run(() =>
            {
                ProcessingAllLines.ProcessLayer(layer, angle);
            });
            CloseButton_Click(sender, e);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            var pane = FrameworkApplication.DockPaneManager.Find("LineCorrection_SmoothenLines");
            pane?.Hide();
        }
    }
}
