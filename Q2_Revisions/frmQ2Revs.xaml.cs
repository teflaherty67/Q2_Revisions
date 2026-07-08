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
using System.Windows.Shapes;

namespace Q2_Revisions
{
    /// <summary>
    /// Interaction logic for frmQ2Revs.xaml
    /// </summary>
    public partial class frmQ2Revs : Window
    {
        public string SpecLevel { get; private set; }

        public frmQ2Revs()
        {
            InitializeComponent();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                this.DragMove();
        }

        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            if (rbCompleteHome.IsChecked == true)
                SpecLevel = "Complete Home";
            else if (rbCompleteHomePlus.IsChecked == true)
                SpecLevel = "Complete Home Plus";
            else
                SpecLevel = "Terrata";

            this.Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            SpecLevel = null;
            this.Close();
        }
    }
}