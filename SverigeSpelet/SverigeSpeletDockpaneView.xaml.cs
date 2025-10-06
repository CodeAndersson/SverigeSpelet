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

namespace SverigeSpelet
{
    /// <summary>
    /// Interaction logic for SverigeSpeletDockpaneView.xaml
    /// </summary>
    public partial class SverigeSpeletDockpaneView : UserControl
    {
        public SverigeSpeletDockpaneView()
        {
            InitializeComponent();
        }

        private void BtnStarta_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            // Denna kod kommer att kopplas till ViewModel
            var viewModel = this.DataContext as SverigeSpeletDockpaneViewModel;
            viewModel?.StartaSpel();
        }

        private void BtnUppdateraTopplista_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var viewModel = this.DataContext as SverigeSpeletDockpaneViewModel;
            viewModel?.UppdateraTopplista();
        }
    }
}
