namespace AntFu7.LiveDraw.Properties
{


    // This class allows you to handle specific events in the parameter class:
    //  The SettingChanging event is triggered before a parameter value is changed.
    //  The PropertyChanged event is raised after changing a parameter value.
    //  The SettingsLoaded event is raised after loading the parameter values.
    //  The SettingsSaving event is triggered before the parameter values ​​are saved.
    internal sealed partial class Settings
    {

        public Settings()
        {
            // // To add event handlers to save and change settings, remove the comment marks from the lines below:
            //
            // this.SettingChanging += this.SettingChangingEventHandler;
            //
            // this.SettingsSaving += this.SettingsSavingEventHandler;
            //
        }

        private void SettingChangingEventHandler(object sender, System.Configuration.SettingChangingEventArgs e)
        {
            // Add code to handle the SettingChangingEvent event here.
        }

        private void SettingsSavingEventHandler(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Add code to handle the SettingsSaving event here.
        }
    }
}
