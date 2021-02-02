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
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using HelixToolkit.Wpf;

namespace CrossBeam
{
    /// <summary>
    /// Window1.xaml 的交互逻辑
    /// </summary>
    public partial class Window1 : Window
    {
        public bool StartPointSelected;
        public bool Done;

        public int cbNumber;
        public double cbDistance;
        public double l1;
        public double h1;
        public double l2;
        public double h2;

        public Window1()
        {
            InitializeComponent();

            //模型导入器
            ModelImporter modelImporter = new ModelImporter();

            //设置材料颜色
            Material material = new DiffuseMaterial(new SolidColorBrush(Colors.AliceBlue));
            modelImporter.DefaultMaterial = material;

            //三维模型导入
            Model3D Model = modelImporter.Load(@"C:\Users\zyx\Desktop\2RevitArcBridge\RevitArc\RevitArc\source\Chord.obj");

            //和modelview设置binding
            Binding binding = new Binding() { Source = Model };
            this.helixviewport.SetBinding(HelixViewport3D.DataContextProperty, binding);

        }


        private void StartPointSelect(object sender, RoutedEventArgs e)
        {
            StartPointSelected = true;
            this.window.Hide();
        }

        private void DoneClick(object sender, RoutedEventArgs e)
        {
            cbNumber = Convert.ToInt32(this.TextcbNumber.Text) ;
            cbDistance = Convert.ToDouble(this.TextcbDistance.Text) *3.28;

            l1 = Convert.ToDouble(this.Texth1.Text) * 3.28;
            h1 = Convert.ToDouble(this.Texth1.Text) * 3.28;
            l2 = Convert.ToDouble(this.Textl2.Text) * 3.28;
            h2 = Convert.ToDouble(this.Texth2.Text) * 3.28;

            Done = true;
            DialogResult = true;
        }


    }
}
