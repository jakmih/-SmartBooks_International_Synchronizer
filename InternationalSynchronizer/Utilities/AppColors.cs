using System.Windows.Media;

namespace InternationalSynchronizer.Utilities
{
    public static class AppColors
    {
        // row colors
        public static readonly SolidColorBrush NEUTRAL_COLOR = new(Colors.LightGray);
        public static readonly SolidColorBrush ACCEPT_COLOR = new(Colors.Lime);
        public static readonly SolidColorBrush DECLINE_COLOR = new(Colors.Red);
        public static readonly SolidColorBrush SYNCED_COLOR = new(Colors.Cyan);
        public static readonly SolidColorBrush DELETED_ITEM_COLOR = new(Colors.Orange);
        public static readonly SolidColorBrush NO_CHILDREN_SYNCED_COLOR = new(Colors.LightGray);
        public static readonly SolidColorBrush ALL_CHILDREN_SYNCED_COLOR = new(Colors.Green);
        public static readonly SolidColorBrush PARTIAL_CHILDREN_SYNCED_COLOR = new(Colors.Yellow);

        // button colors
        public static readonly SolidColorBrush BACK_BUTTON_COLOR = new(Colors.Gray);
        public static readonly SolidColorBrush AUTO_SYNC_BUTTON_COLOR = new(Colors.Cyan);
        public static readonly SolidColorBrush CONFIRM_BUTTON_COLOR = new(Colors.Lime);
        public static readonly SolidColorBrush MANUAL_SYNC_BUTTON_COLOR = new(Colors.Yellow);
        public static readonly SolidColorBrush DELETE_SYNC_BUTTON_COLOR = new(Colors.Red);
        public static readonly SolidColorBrush CHOOSE_NEW_DATABASES_BUTTON_COLOR = new(Colors.Magenta);
    }
}
