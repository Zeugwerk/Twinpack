using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Twinpack.Models
{
    public class PackagingServer : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public string Name { get; set; }
        public string Url { get; set; }
        public string ServerType { get; set; }

        bool _loggedIn;
        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public bool LoggedIn
        {
            get
            {
                return _loggedIn && _connected;
            }
            set
            {
                _loggedIn = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LoggedIn)));
            }
        }

        bool _connected;
        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public bool Connected
        {
            get
            {
                return _connected;
            }
            set
            {
                _connected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Connected)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LoggedIn)));
            }
        }
    }
}
