using System;
using System.Globalization;
using System.Windows.Data;

namespace LocoSwap.Converters
{
    [ValueConversion(typeof(VehicleExistance), typeof(string))]
    public class VehicleStatusToImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return string.Empty;
            var status = (VehicleExistance)value;
            string image;
            switch (status)
            {
                case VehicleExistance.Found:
                    image = "BulletGreen.png";
                    break;
                case VehicleExistance.Replaced:
                    image = "Replaced.png";
                    break;
                case VehicleExistance.MissingWithRule:
                    image = "BulletYellow.png";
                    break;
                case VehicleExistance.Missing:
                default:
                    image = "BulletRed.png";
                    break;
            }
            string uri = String.Format("/LocoSwap;component/Resources/{0}", image);
            return uri;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }


    [ValueConversion(typeof(ConsistVehicleExistance), typeof(string))]
    public class ConsistStatusToImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return string.Empty;
            var status = (ConsistVehicleExistance)value;
            string image;
            switch (status)
            {
                case ConsistVehicleExistance.Found:
                    image = "BulletGreen.png";
                    break;
                case ConsistVehicleExistance.FullyReplaced:
                    image = "ReplacedGreen.png";
                    break;
                case ConsistVehicleExistance.PartiallyReplaced:
                    image = "ReplacedRed.png";
                    break;
                case ConsistVehicleExistance.MissingWithRulesPartiallyReplaced:
                    image = "ReplacedYellow.png";
                    break;
                case ConsistVehicleExistance.MissingWithRules:
                    image = "BulletYellow.png";
                    break;
                case ConsistVehicleExistance.Missing:
                default:
                    image = "BulletRed.png";
                    break;
            }
            string uri = String.Format("/LocoSwap;component/Resources/{0}", image);
            return uri;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    [ValueConversion(typeof(ScenarioVehicleExistance), typeof(string))]
    public class ScenarioStatusToImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return string.Empty;
            var status = (ScenarioVehicleExistance)value;
            string image;
            switch (status)
            {
                case ScenarioVehicleExistance.Found:
                    image = "BulletGreen.png";
                    break;
                case ScenarioVehicleExistance.FullyReplaced:
                    image = "ReplacedGreen.png";
                    break;
                case ScenarioVehicleExistance.PartiallyReplaced:
                    image = "ReplacedRed.png";
                    break;
                case ScenarioVehicleExistance.MissingWithRulesPartiallyReplaced:
                    image = "ReplacedYellow.png";
                    break;
                case ScenarioVehicleExistance.MissingWithRules:
                    image = "BulletYellow.png";
                    break;
                case ScenarioVehicleExistance.Missing:
                default:
                    image = "BulletRed.png";
                    break;
            }
            string uri = String.Format("/LocoSwap;component/Resources/{0}", image);
            return uri;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
