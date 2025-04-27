using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Winhance.WPF.Features.Common.Resources;

namespace Winhance.WPF.Features.Common.Controls
{
    /// <summary>
    /// Interaction logic for MaterialSymbol.xaml
    /// </summary>
    public partial class MaterialSymbol : UserControl
    {
        public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
            "Icon", typeof(string), typeof(MaterialSymbol), 
            new PropertyMetadata(string.Empty, OnIconChanged));

        public static readonly DependencyProperty IconTextProperty = DependencyProperty.Register(
            "IconText", typeof(string), typeof(MaterialSymbol), 
            new PropertyMetadata(string.Empty));

        public string Icon
        {
            get { return (string)GetValue(IconProperty); }
            set { SetValue(IconProperty, value); }
        }

        public string IconText
        {
            get { return (string)GetValue(IconTextProperty); }
            set { SetValue(IconTextProperty, value); }
        }

        public MaterialSymbol()
        {
            InitializeComponent();
        }

        private static void OnIconChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MaterialSymbol control && e.NewValue is string iconName)
            {
                control.IconText = MaterialSymbols.GetIcon(iconName);
            }
        }
    }
}
