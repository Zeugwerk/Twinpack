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
                return _loggedIn && _connected && !_connecting;
            }
            set
            {
                _loggedIn = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LoggedIn)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LoggedOut)));
            }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public bool LoggedOut
        {
            get
            {
                return !_loggedIn && !_connecting;
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
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LoggedOut)));
            }
        }

        bool _connecting;
        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public bool Connecting
        {
            get
            {
                return _connecting;
            }
            set
            {
                _connecting = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Connecting)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LoggedIn)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LoggedOut)));
            }
        }
    }
}
