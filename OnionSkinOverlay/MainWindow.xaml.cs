//https://sourceforge.net/p/nikoncswrapper/wiki/Getting%20Started/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.WindowsAPICodePack.Dialogs;
using Nikon;

namespace OnionSkinOverlay
{
    public partial class MainWindow : Window
    {
        #region --- Declarations ---
        private NikonManager manager;
        private NikonDevice device;
        private Timer liveViewTimer, batteryTimer;

        private int imageruncounter = 0;
        private int imagecounter;
        string file_name = "";
        private bool device_ready = false;

        private Rect _location { get; set; }
        #endregion

        #region --- Constructors ---
        public MainWindow()
        {
            InitializeComponent();

            // Disable buttons
            device_ready = false;
            ToggleButtons();

            // Initialize live view timer
            liveViewTimer = new Timer();
            liveViewTimer.Tick += new EventHandler(liveViewTimer_Tick);
            liveViewTimer.Interval = 1000 / 30;

            batteryTimer = new Timer();
            batteryTimer.Tick += new EventHandler(batteryTimer_Tick);
            batteryTimer.Interval = 30000;

            // Initialize Nikon manager
            bool is64 = Environment.Is64BitProcess;

            String md3 = "";

            if (is64)
            {
                md3 = "Nikon SDK\\Binary Files\\x64\\Type0003.md3";
            } else
            {
                md3 = "Nikon SDK\\Binary Files\\x86\\Type0003.md3";
            }

            manager = new NikonManager(md3);
            manager.DeviceAdded += new DeviceAddedDelegate(manager_DeviceAdded);
            manager.DeviceRemoved += new DeviceRemovedDelegate(manager_DeviceRemoved);

        }
        #endregion  

        #region --- Properties ---
        private Rect DesktopArea
        {
            get
            {
                var c = System.Windows.Forms.Cursor.Position;
                var s = System.Windows.Forms.Screen.FromPoint(c);
                var a = s.WorkingArea;
                return new Rect(a.Left, a.Top, a.Width, a.Height);
            }
        }
        #endregion

        #region --- Dependency Properties ---
        public static readonly DependencyProperty InternalWindowStateProperty = DependencyProperty.Register("InternalWindowState", typeof(WindowState), typeof(MainWindow), new PropertyMetadata(WindowState.Normal, new PropertyChangedCallback(OnInternalWindowStateChanged)));
        private string currentDirectory;

        public WindowState InternalWindowState
        {
            get { return (WindowState)GetValue(InternalWindowStateProperty); }
            set { SetValue(InternalWindowStateProperty, value); }
        }

        private static void OnInternalWindowStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            MainWindow instance = (MainWindow)d;
            instance.SetInternalWindowState((WindowState)e.NewValue);
        }
        #endregion

        #region --- Private Methods ---
        private void StoreLocation()
        {
            _location = new Rect(this.Left, this.Top, this.Width, this.Height);
        }

        private void RestoreLocation()
        {
            this.Width = _location.Width;
            this.Height = _location.Height;
            this.Top = _location.Top >= 0 ? _location.Top : 0;
            this.Left = _location.Left;
        }

        private void SetMaximizedState()
        {
            this.Width = DesktopArea.Width;
            this.Height = DesktopArea.Height;
            this.Top = DesktopArea.Top;
            this.Left = DesktopArea.Left;
        }

        private void SetInternalWindowState(WindowState state)
        {
            InternalWindowState = state;

            switch (InternalWindowState)
            {
                case WindowState.Normal:
                    this.WindowState = WindowState.Normal;
                    RestoreLocation();
                    break;
                case WindowState.Maximized:
                    this.WindowState = WindowState.Normal;
                    SetMaximizedState();
                    break;
                case WindowState.Minimized:
                    this.WindowState = WindowState.Minimized;
                    break;
            }
        }
        #endregion

        #region --- Sizing Routines --- 
        private void Thumb_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            Thumb thumb = (Thumb)sender;
            int tag = Convert.ToInt32(thumb.Tag);
            if (thumb.Cursor == System.Windows.Input.Cursors.SizeWE) HandleSizeWE(tag, e);
            if (thumb.Cursor == System.Windows.Input.Cursors.SizeNS) HandleSizeNS(tag, e);
            if (thumb.Cursor == System.Windows.Input.Cursors.SizeNESW) HandleSizeNESW(tag, e);
            if (thumb.Cursor == System.Windows.Input.Cursors.SizeNWSE) HandleSizeNWSE(tag, e);
        }

        private void HandleSizeNWSE(int tag, DragDeltaEventArgs e)
        {
            if (tag == 0)
            {
                this.Top += e.VerticalChange;
                this.Height -= e.VerticalChange;
                this.Left += e.HorizontalChange;
                this.Width -= e.HorizontalChange;
            }
            else
            {
                this.Width += e.HorizontalChange;
                this.Height += e.VerticalChange;
            }
        }

        private void HandleSizeNESW(int tag, DragDeltaEventArgs e)
        {
            if (tag == 0)
            {
                this.Top += e.VerticalChange;
                this.Height -= e.VerticalChange;
                this.Width += e.HorizontalChange;
            }
            else
            {
                this.Left += e.HorizontalChange;
                this.Width -= e.HorizontalChange;
                this.Height += e.VerticalChange;
            }
        }

        private void HandleSizeNS(int tag, DragDeltaEventArgs e)
        {
            if (tag == 0)
            {
                this.Top += e.VerticalChange;
                this.Height -= e.VerticalChange;
            }
            else
                this.Height += e.VerticalChange;
        }

        private void HandleSizeWE(int tag, DragDeltaEventArgs e)
        {
            if (tag == 0)
            {
                this.Left += e.HorizontalChange;
                this.Width -= e.HorizontalChange;
            }
            else
                this.Width += e.HorizontalChange;
        }
        #endregion

        #region --- OnClose ---
        protected override void OnClosed(EventArgs e)
        {
            manager.Shutdown();
            base.OnClosed(e);
        }
        #endregion

        #region --- Event Handlers ---
        private void OnDragMoveWindow(Object sender, MouseButtonEventArgs e)
        {
            if (this.InternalWindowState == WindowState.Maximized)
            {
                var c = System.Windows.Forms.Cursor.Position;
                this.InternalWindowState = WindowState.Normal;
                this.Height = _location.Height;
                this.Width = _location.Width;
                this.Top = c.Y - (titleBar.ActualHeight / 2);
                this.Left = c.X - (_location.Width / 2);
            }
            this.DragMove();
        }

        private void OnMaximizeWindow(Object sender, MouseButtonEventArgs e)
        {
            if (this.InternalWindowState == WindowState.Maximized)
                this.InternalWindowState = WindowState.Normal;
            else
                this.InternalWindowState = WindowState.Maximized;
        }

        private void OnMinimizeWindow(Object sender, MouseButtonEventArgs e)
        {
            this.InternalWindowState = WindowState.Minimized;
        }


        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
            {
                this.InternalWindowState = WindowState.Maximized;
            }
        }

        private void OnCloseWindow(Object sender, MouseButtonEventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
        }


        private void Window_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (this.InternalWindowState != WindowState.Maximized)
                StoreLocation();
        }
        #endregion


        void device_CaptureComplete(NikonDevice sender, int data)
        {
            // Re-enable buttons when the capture completes
            device_ready = true;
            ToggleButtons();
        }

        void liveViewTimer_Tick(object sender, EventArgs e)
        {
            // Get live view image
            NikonLiveViewImage image = null;

            try
            {
                image = device.GetLiveViewImage();
            }
            catch (NikonException)
            {
                liveViewTimer.Stop();
            }

            // Set live view image on picture box
            if (image != null)
            {
                MemoryStream stream = new MemoryStream(image.JpegBuffer);

                BitmapImage bi = new BitmapImage();
                bi.BeginInit();
                bi.StreamSource = stream;
                bi.EndInit();
                image_LiveView.Source = bi;

            }
        }

        void manager_DeviceRemoved(NikonManager sender, NikonDevice device)
        {
            this.device = null;

            // Stop live view timer
            liveViewTimer.Stop();

            // Clear device name
            label_devicename.Content = "Kein Gerät verbunden";

            // Disable buttons
            device_ready = false;
            ToggleButtons();
            batteryTimer.Stop();

            // Clear live view picture
            image_LiveView.Source = null;
        }

        //Neues Gerät erkannt
        void manager_DeviceAdded(NikonManager sender, NikonDevice device)
        {
            this.device = device;

            // Set the device name
            label_devicename.Content = device.Name;

            // Enable buttons
            device_ready = true;
            ToggleButtons();

            //Enable Battery watcher
            batteryTimer.Start();

            // Hook up device capture events
            device.ImageReady += new ImageReadyDelegate(device_ImageReady);
            device.CaptureComplete += new CaptureCompleteDelegate(device_CaptureComplete);
        }


        //Buttons DeAktivieren
        void ToggleButtons()
        {
            bool enabled = device_ready;
            if (currentDirectory == null) //Wenn Speicherort nicht gesetzt
            {
                enabled = false;
            }
            this.checkBox_liveview.IsEnabled = enabled;
            this.button_aufnahme.IsEnabled = enabled;
        }


        //LiveView Handler
        private void checkbox_liveview_UnChecked(object sender, EventArgs e)
        {
            deaktivate_liveview();
           
        }
        private void deaktivate_liveview()
        {
            if (device == null)
            {
                return;
            }

            device.LiveViewEnabled = false;
            liveViewTimer.Stop();
            image_LiveView.Source = null;
        }
        private void checkbox_liveview_Checked(object sender, EventArgs e)
        {
            aktivate_liveview();
        }
        private void aktivate_liveview()
        {
            if (device == null)
            {
                return;
            }

            device.LiveViewEnabled = true;
            liveViewTimer.Start();
        }

        //FolderCanger
        private void Button_ChangeFolderClick(object sender, RoutedEventArgs e)
        {
            
            var dlg = new CommonOpenFileDialog();
            dlg.Title = "Zu überwachenden Ordner wählen...";
            dlg.IsFolderPicker = true;
            dlg.InitialDirectory = currentDirectory;

            dlg.AddToMostRecentlyUsedList = false;
            dlg.AllowNonFileSystemItems = false;
            dlg.DefaultDirectory = currentDirectory;
            dlg.EnsureFileExists = true;
            dlg.EnsurePathExists = true;
            dlg.EnsureReadOnly = false;
            dlg.EnsureValidNames = true;
            dlg.Multiselect = false;
            dlg.ShowPlacesList = true;

            if (dlg.ShowDialog() == CommonFileDialogResult.Ok)
            {
                var folder = dlg.FileName;
                currentDirectory = folder;
                Console.WriteLine("Ordner zu " + currentDirectory + " geändert");
                FileSystemWatcher watcher = new FileSystemWatcher();
                watcher.Path = folder;
                //watcher.NotifyFilter = NotifyFilters.LastWrite;  //Nicht nötig
                watcher.Filter = "*.*";
                watcher.Changed += new FileSystemEventHandler(OnChangedFolder);
                watcher.Created += new FileSystemEventHandler(OnChangedFolder);
                watcher.Renamed += new RenamedEventHandler(OnChangedFolder); //Testweise
                watcher.EnableRaisingEvents = true;

                spinner_Scanning.Visibility = Visibility.Visible;

                ToggleButtons();
            }
        }
        private void OnChangedFolder(object sender, FileSystemEventArgs e)
        {
            FileInfo filetocheck = new FileInfo(e.FullPath);

            if (filetocheck.Extension.Equals(".jpg") || filetocheck.Extension.Equals(".jepg") || filetocheck.Extension.Equals(".png"))
            {

                Console.WriteLine("Bildänderung");

                //This will lock the execution until the file is ready            
                bool ergebnis = false;
                while (!ergebnis)
                {
                    try
                    {
                        using (System.IO.File.Open(filetocheck.FullName,FileMode.Open, FileAccess.Read, FileShare.Read))
                        ergebnis = true;
                    }
                    catch (Exception)
                    {
                        System.Threading.Thread.Sleep(10);
                        ergebnis = false;
                    }
                }

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(filetocheck.FullName);
                bitmap.EndInit();
                bitmap.Freeze();

                this.Dispatcher.Invoke(() =>
                {
                    image_OnionLayer.Source = bitmap;
                });

                Console.WriteLine("Bild wird angezeigt");


            }

        }

        private void button_aufnahme_Click(object sender, RoutedEventArgs e)
        {
            deaktivate_liveview();
            if (device == null)
            {
                return;
            }

            device_ready = false;
            ToggleButtons();

            try
            {
                device.Capture();
            }
            catch (NikonException ex)
            {
                device_ready = true;
                ToggleButtons();

                if (checkBox_liveview.IsChecked == true)
                {
                    aktivate_liveview();
                }
            }

            image_LiveView.Source = null;
        }
        void device_ImageReady(NikonDevice sender, NikonImage image)
        {
            string extension = "";

            if (imageruncounter == 0)
            {
                string suffix = "";
                string mitte = "";

                //Mitte
                if (comboBox_Filename_Mitte.SelectedItem != null)
                {
                    int itemindex = comboBox_Filename_Mitte.SelectedIndex;

                    switch (itemindex)
                    {
                        case -1: // Nichts gewählt
                            break;

                        case 0: // Nummern  
                            imagecounter = imagecounter + 1;
                            mitte = imagecounter.ToString("D3");
                            break;

                        case 1: // Datum
                            mitte = DateTime.Now.ToString("dd.MM.yyyy");
                            break;

                        case 2: // Urzeit
                            mitte = DateTime.Now.ToString("HH.mm.ss");
                            break;

                    }
                }

                //Suffix
                if (comboBox_Filename_Suffix.SelectedItem != null)
                {
                    int itemindex = comboBox_Filename_Suffix.SelectedIndex;

                    switch (itemindex)
                    {
                        case -1: // Nichts gewählt
                            break;

                        case 0: // Nichts gewählt
                            break;

                        case 1: // Nummern  
                            imagecounter = imagecounter + 1;
                            mitte = imagecounter.ToString("D3");
                            break;

                        case 2: // Datum
                            mitte = DateTime.Now.ToString("dd.MM.yyyy");
                            break;

                        case 3: // Urzeit
                            mitte = DateTime.Now.ToString("HH.mm.ss");
                            break;

                    }
                }
         
                extension = ".jpg";

                file_name = currentDirectory + "\\" + textBox_prefix.Text + mitte + suffix;

                if (File.Exists(file_name + extension))
                {
                    file_name = currentDirectory + "\\" + textBox_prefix.Text + mitte + suffix + "_new_";
                }

                imageruncounter = 1;
            }
            else
            {
                extension = ".raw";
                imageruncounter = 0;
            }

            
            using (FileStream stream = new FileStream(file_name + extension, FileMode.Create, FileAccess.Write))
            {
                stream.Write(image.Buffer, 0, image.Buffer.Length);
            }

            if (imageruncounter == 0 && checkBox_liveview.IsChecked == true)
            {
                aktivate_liveview();
            }
        }

        //Always on Top Checkbox
        private void CheckBox_AlwaysOnTop_Checked(object sender, RoutedEventArgs e)
        {
            bool isChecked = (checkBox_AlwaysOnTop.IsChecked == true);
            if (isChecked)
            {
                this.Topmost = true;
            }
            else
            {
                this.Topmost = false;
            }
        }

        
        //Menü öffnen und Schließen
        private void ToggleButton_Checked(object sender, RoutedEventArgs e)
        {
            ThicknessAnimation tahauptmenu = new ThicknessAnimation();
            tahauptmenu.From = MainSettingsGrid.Margin;
            tahauptmenu.To = new Thickness(0, 80, 260, 70);
            tahauptmenu.Duration = new Duration(TimeSpan.FromMilliseconds(150));
            MainSettingsGrid.BeginAnimation(Grid.MarginProperty, tahauptmenu);
        }

        private void ToggleButton_UnChecked(object sender, RoutedEventArgs e)
        {
            ThicknessAnimation tahauptmenu = new ThicknessAnimation();
            tahauptmenu.From = MainSettingsGrid.Margin;
            tahauptmenu.To = new Thickness(-260, 80, 0, 70);
            tahauptmenu.Duration = new Duration(TimeSpan.FromMilliseconds(150));
            MainSettingsGrid.BeginAnimation(Grid.MarginProperty, tahauptmenu);
        }


        //Battery Level
        void batteryTimer_Tick(object sender, EventArgs e)
        {
            int battery_level = getBatteryLevel();
            if (battery_level < 50)
            {
                label_battery.Foreground = System.Windows.Media.Brushes.Yellow;
            } else if (battery_level < 15)
            {
                label_battery.Foreground = System.Windows.Media.Brushes.Red;

            } else if (battery_level > 50)
            {
                label_battery.Foreground = System.Windows.Media.Brushes.White;
            }
            label_battery.Content = "Akku: " + battery_level.ToString("D2") + " %";
        }
        private int getBatteryLevel()
        {
            try
            {
                int batteryLevel = device.GetInteger(eNkMAIDCapability.kNkMAIDCapability_BatteryLevel);
                return batteryLevel;
            }
            catch (NikonException ex)
            {
                return 0;
            }
        }
    }
}