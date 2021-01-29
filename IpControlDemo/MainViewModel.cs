using System.Net;

namespace IpControlHarness
{
    public class MainViewModel : ViewModelBase
    {

        private IPAddress _ipv4Address;
        public IPAddress IPV4Address
        {
            get => _ipv4Address;
            set
            {
                SetProperty(ref _ipv4Address, value);
                IPV4String = _ipv4Address?.ToString() ?? new IPAddress(new byte[4]).ToString();
            }
        }

        private string _ipv4String;
        public string IPV4String
        {
            get => _ipv4String;
            set => SetProperty(ref _ipv4String, value);
        }

        private IPAddress _ipv6Address;
        public IPAddress IPV6Address
        {
            get => _ipv6Address;
            set
            {
                SetProperty(ref _ipv6Address, value);
                IPV6String = _ipv6Address?.ToString() ?? new IPAddress(new byte[16]).ToString();
            }
        }

        private string _ipv6String;
        public string IPV6String
        {
            get => _ipv6String;
            set => SetProperty(ref _ipv6String, value);
        }

        private bool _ipv6Valid;
        public bool IPV6Valid
        {
            get => _ipv6Valid;
            set => SetProperty(ref _ipv6Valid, value);
        }
    }
}
