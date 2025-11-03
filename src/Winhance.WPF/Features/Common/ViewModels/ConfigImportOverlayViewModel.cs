using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Winhance.WPF.Features.Common.ViewModels
{
    public class ConfigImportOverlayViewModel : INotifyPropertyChanged
    {
        private string _statusText;
        private string _detailText;

        public string StatusText
        {
            get => _statusText;
            set
            {
                _statusText = value;
                OnPropertyChanged();
            }
        }

        public string DetailText
        {
            get => _detailText;
            set
            {
                _detailText = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
