using System;
using System.Diagnostics;
using System.Management.Automation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Huddled.Interop.Hotkeys;
using Huddled.WPF.Controls;
//using PoshConsole.Controls;
using PoshConsole.Controls;
using PoshConsole.PSHost;
using IPoshConsoleControl=Huddled.WPF.Controls.Interfaces.IPoshConsoleControl;
using IPSConsole=Huddled.WPF.Controls.Interfaces.IPSConsole;
using System.Windows.Documents;

namespace PoshConsole
{

   /// <summary>
   /// Implementation of a WPF host for PowerShell
   /// </summary>
   public partial class PoshConsoleWindow : System.Windows.Window, IPSUI
   {

      #region  Fields (11)

      private DoubleAnimation _hideHeightAnimations = new DoubleAnimation(0.0, _lasts);
      private DoubleAnimation _hideOpacityAnimations = new DoubleAnimation(0.0, _lasts);
      private bool _isHiding = false;
      private static Duration _lasts = Duration.Automatic;
      private DoubleAnimation _showHeightAnimation = new DoubleAnimation(1.0, _lasts);
      private DoubleAnimation _showOpacityAnimation = new DoubleAnimation(1.0, _lasts);
      private static DiscreteBooleanKeyFrame _trueEndFrame = new DiscreteBooleanKeyFrame(true, KeyTime.FromPercent(1.0));
      private static DiscreteObjectKeyFrame _visKeyHidden = new DiscreteObjectKeyFrame(Visibility.Hidden, KeyTime.FromPercent(1.0));
      private static DiscreteObjectKeyFrame _visKeyVisible = new DiscreteObjectKeyFrame(Visibility.Visible, KeyTime.FromPercent(0.0));

      private static DependencyProperty _consoleProperty;
      static PoshConsoleWindow()
      {
         try
         {
            _consoleProperty = DependencyProperty.Register("Console", typeof(IPoshConsoleControl), typeof(PoshConsoleWindow));
         }
         catch (Exception ex)
         {
            Trace.WriteLine(ex.Message);
         }
      }

      /// <summary>
      /// The PSHost implementation for this interpreter.
      /// </summary>
      private PoshHost _host;

      #endregion 

      #region  Constructors (1)

      /// <summary>
      /// Initializes a new instance of the <see cref="PoshConsole"/> class.
      /// </summary>
      public PoshConsoleWindow()
      {
         Cursor = Cursors.AppStarting;

         // Create the host and runspace instances for this interpreter. Note that
         // this application doesn't support console files so only the default snapins
         // will be available.

         InitializeComponent();

         // "buffer" is defined in the XAML
         this.Console = buffer;
         

         // before we start animating, set the animation endpoints to the current values.
         _hideOpacityAnimations.From = _showOpacityAnimation.To = Opacity;
         _hideHeightAnimations.From = _showHeightAnimation.To = this.Height;

         // buffer.TitleChanged += new passDelegate<string>(delegate(string val) { Title = val; });
         Properties.Settings.Default.PropertyChanged += new System.ComponentModel.PropertyChangedEventHandler(SettingsPropertyChanged);

         // problems with data binding 
         WindowStyle = Properties.Settings.Default.WindowStyle;
         if (Properties.Settings.Default.WindowStyle == WindowStyle.None)
         {
            AllowsTransparency = true;
            ResizeMode = ResizeMode.CanResizeWithGrip;
         }
         else
         {
            // we ignore the border if the windowstyle isn't "None"
            border.BorderThickness = new Thickness(0D, 0D, 0D, 0D);
            ResizeMode = ResizeMode.CanResize;
         }

      }

      #endregion 

      #region  Properties (1)

      public IPoshConsoleControl Console
      {
         get { return ((IPoshConsoleControl)base.GetValue(_consoleProperty)); }
         set { base.SetValue(_consoleProperty, value); }
      }

      #endregion 

      #region  Delegates and Events (5)

      //  Delegates (5)

      // Universal Delegates
      internal delegate void PassDelegate<T>(T input);
      internal delegate RET PassReturnDelegate<T, RET>(T input);
      internal delegate RET ReturnDelegate<RET>();
      private delegate void SettingsChangedDelegate(object sender, System.ComponentModel.PropertyChangedEventArgs e);
      private delegate void VoidVoidDelegate();

      #endregion 

      #region  Methods (9)

      //  Protected Methods (2)

      /// <summary>
      /// Raises the <see cref="E:System.Windows.Window.Closing"></see> event, and executes the ShutdownProfile
      /// </summary>
      /// <param name="e">A <see cref="T:System.ComponentModel.CancelEventArgs"></see> that contains the event data.</param>
      protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
      {
         ((IPSConsole)buffer).WriteVerboseLine("Running Exit Scripts...");
         if (_host != null) _host.ExecuteShutdownProfile();
         ((IPSConsole)buffer).WriteVerboseLine("Shutting Down.");
         // // This doesn't fix the COM RCW problem
         // Dispatcher.Invoke((Action)(() => { _host.KillConsole(); }));
         _host.KillConsole();

         base.OnClosing(e);
      }

      protected override void OnSourceInitialized(EventArgs e)
      {
         base.OnSourceInitialized(e);

         // so now we can ask which keys are still unregistered.
         HotkeyManager hk = HotkeyManager.GetHotkeyManager(this);
         int k = -1;
         while (++k < hk.UnregisteredKeys.Count)
         {
            KeyBinding key = hk.UnregisteredKeys[k];
            // TODO: A GUI for changing the hotkeys... 

            // but you could try modifying them yourself ...
            ModifierKeys mk = HotkeyManager.FindUnsetModifier(key.Modifiers);
            if (mk != ModifierKeys.None)
            {
               MessageBox.Show(string.Format("Unable to register hotkey: {0}+{1} \nfor {2}\n\nWe'll try registering it as {3}+{0}+{1}.", key.Modifiers, key.Key, key.Command, mk));
               key.Modifiers |= mk;
               hk.Add(key);
            }
            else
            {
               MessageBox.Show(string.Format("Failed to register the hotkey: {0}+{1} \nfor {2}.", key.Modifiers, key.Key, key.Command));
            }
         }
      }


      public override void EndInit()
      {
         base.EndInit();
         // LOAD the startup banner only when it's set (instead of removing it after)
         if (Properties.Settings.Default.StartupBanner && System.IO.File.Exists("StartupBanner.xaml"))
         {
            try
            {
               Paragraph banner;
               System.Management.Automation.ErrorRecord error;
               System.IO.FileInfo startup = new System.IO.FileInfo("StartupBanner.xaml");
               if (startup.TryLoadXaml(out banner, out error))
               {
                  // Copy over *all* resources from the DOCUMENT to the BANNER
                  // NOTE: be careful not to put resources in the document you're not willing to expose
                  // NOTE: this will overwrite resources with matching keys, so banner-makers need to be aware
                  foreach (string key in buffer.Document.Resources.Keys)
                  {
                     banner.Resources[key] = buffer.Document.Resources[key];
                  }
                  banner.Padding = new Thickness(5);
                  buffer.Document.Blocks.InsertBefore(buffer.Document.Blocks.FirstBlock,banner);
                  //_current = new Paragraph();
                  //_current.ClearFloaters = WrapDirection.Both;
                  //buffer.Document.Blocks.Add(_current);
               }
               else
               {
                  ((IPSConsole)buffer).Write("PoshConsole 1.0 2008.09.01");
               }

               // Document.Blocks.InsertBefore(Document.Blocks.FirstBlock, new Paragraph(new Run("PoshConsole`nVersion 1.0.2007.8150")));
               // Document.Blocks.AddRange(LoadXamlBlocks("StartupBanner.xaml"));
            }
            catch (Exception ex)
            {
               System.Diagnostics.Trace.TraceError(@"Problem loading StartupBanner.xaml\n{0}", ex.Message);
               buffer.Document.Blocks.Clear();
               ((IPSConsole)buffer).Write("PoshConsole 1.0 2008.09.01");
            }
         }
         else
         {
            ((IPSConsole)buffer).Write("PoshConsole 1.0 2008.09.01");
         }
      }
	
      //private void buffer_SizeChanged(object sender, SizeChangedEventArgs e)
      //{
      //    RecalculateSizes();
      //}
      protected override void OnGotFocus(RoutedEventArgs e)
      {
         buffer.Focus();
         base.OnGotFocus(e);
      }

      //  Private Methods (7)

      /// <summary>
      /// Hides the window.
      /// </summary>
      private void HideWindow()
      {
         _isHiding = true;
         if (Properties.Settings.Default.Animate)
         {
            ObjectAnimationUsingKeyFrames visi = new ObjectAnimationUsingKeyFrames();
            visi.Duration = _lasts;
            visi.KeyFrames.Add(_visKeyHidden);

            _hideOpacityAnimations.AccelerationRatio = 0.25;
            _hideHeightAnimations.AccelerationRatio = 0.5;
            _showOpacityAnimation.AccelerationRatio = 0.25;
            _showHeightAnimation.AccelerationRatio = 0.5;

            // before we start animating, set the animation endpoints to the current values.
            _hideOpacityAnimations.From = _showOpacityAnimation.To = (double)this.GetAnimationBaseValue(OpacityProperty);
            _hideHeightAnimations.From = _showHeightAnimation.To = (double)this.GetAnimationBaseValue(HeightProperty);

            // GO!
            this.BeginAnimation(HeightProperty, _hideHeightAnimations, HandoffBehavior.SnapshotAndReplace);
            this.BeginAnimation(OpacityProperty, _hideOpacityAnimations, HandoffBehavior.SnapshotAndReplace);
            this.BeginAnimation(VisibilityProperty, visi, HandoffBehavior.SnapshotAndReplace);
         }
         else
         {
            Hide();
         }
      }

      void IPSUI.SetShouldExit(int exitCode)
      {
         Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, (Action)(() => Application.Current.Shutdown(exitCode)));
      }

      ///// <summary>
      ///// Handles the HotkeyPressed event from the Hotkey Manager
      ///// </summary>
      ///// <param name="window">The window.</param>
      ///// <param name="hotkey">The hotkey.</param>
      //void Hotkey_Pressed(Window window, Hotkey hotkey)
      //{
      //    if(hotkey.Equals(FocusKey))
      //    {
      //        if(!IsActive)
      //        {
      //            Activate(); // Focus();
      //        }
      //        else
      //        {
      //            // if they used the hotkey while the window has focus, they want it to hide...
      //            // but we only need to do that HERE if AutoHide is false 
      //            // if AutoHide is true, it hides during the Deactivate handler
      //            if (Properties.Settings.Default.AutoHide == false) HideWindow();
      //            NativeMethods.ActivateNextWindow(NativeMethods.GetWindowHandle(this));
      //        }
      //    }
      //}
      private void OnAdminMouseDoubleClick(object sender, MouseButtonEventArgs e)
      {
         if (!PoshConsole.Interop.NativeMethods.IsUserAnAdmin())
         {
            Process current = Process.GetCurrentProcess();

            Process proc = new Process();
            proc.StartInfo = new ProcessStartInfo();
            //proc.StartInfo.CreateNoWindow = true;
            proc.StartInfo.UseShellExecute = true;
            proc.StartInfo.Verb = "RunAs";
            proc.StartInfo.FileName = current.MainModule.FileName;
            proc.StartInfo.Arguments = current.StartInfo.Arguments;
            try
            {
               if (proc.Start())
               {
                  this._host.SetShouldExit(0);
               }
            }
            catch (System.ComponentModel.Win32Exception we)
            {
               // if( w32.Message == "The operation was canceled by the user" )
               // if( w32.NativeErrorCode == 1223 ) {
               ((IPSConsole)buffer).WriteErrorLine("Error Starting new instance:" + we.Message);
               // myHost.Prompt();
            }
         }
      }

      #region IPSUI Members

      void IPSUI.WriteProgress(long sourceId, ProgressRecord record)
      {
         if (!Dispatcher.CheckAccess())
         {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)(() => ((IPSUI) this).WriteProgress(sourceId, record)));
         }
         else
         {
            if (record.RecordType == ProgressRecordType.Completed)
            {
               progress1.Visibility = Visibility.Collapsed;
            }
            else
            {
               progress1.Visibility = Visibility.Visible;

               progress1.Activity = record.Activity;
               progress1.Status = record.StatusDescription;
               progress1.Operation = record.CurrentOperation;
               progress1.PercentComplete = record.PercentComplete;
               progress1.TimeRemaining = TimeSpan.FromSeconds(record.SecondsRemaining);
            }
         }
      }

      PSCredential IPSUI.PromptForCredential(string caption, string message, string userName, string targetName, PSCredentialTypes allowedCredentialTypes, PSCredentialUIOptions options)
      {
         throw new Exception("The method or operation is not implemented.");
      }

      PSCredential IPSUI.PromptForCredential(string caption, string message, string userName, string targetName)
      {
         throw new Exception("The method or operation is not implemented.");
      }

      System.Security.SecureString IPSUI.ReadLineAsSecureString()
      {
         throw new Exception("The method or operation is not implemented.");
      }

      #endregion


      private void OnCanHandleControlC(object sender, CanExecuteRoutedEventArgs e)
      {
         e.CanExecute = true;
      }

      private void OnHandleControlC(object sender, ExecutedRoutedEventArgs e)
      {
         try
         {
            _host.StopPipeline();
            e.Handled = true;
         }
         catch (Exception exception)
         {
            _host.UI.WriteErrorLine(exception.ToString());
         }

      }

      /// <summary>
      /// Handles the Activated event of the Window control.
      /// </summary>
      /// <param name="sender">The source of the event.</param>
      /// <param name="e">The <see cref="System.Windows.RoutedEventArgs"/> instance containing the event data.</param>
      private void OnWindowActivated(object sender, EventArgs e)
      {
         if (_isHiding)
         {
            if (Properties.Settings.Default.Animate)
            {
               ObjectAnimationUsingKeyFrames visi = new ObjectAnimationUsingKeyFrames();
               visi.Duration = _lasts;
               visi.KeyFrames.Add(_visKeyVisible);

               // Go!
               this.BeginAnimation(HeightProperty, _showHeightAnimation, HandoffBehavior.SnapshotAndReplace);
               this.BeginAnimation(OpacityProperty, _showOpacityAnimation, HandoffBehavior.SnapshotAndReplace);
               this.BeginAnimation(VisibilityProperty, visi, HandoffBehavior.SnapshotAndReplace);
            }
            else
            {
               Show();
            }
         }
         if (Properties.Settings.Default.Animate)
         {
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new VoidVoidDelegate(delegate { buffer.Focus(); }));
         }
         else
         {
            buffer.Focus();
         }
      }

      /// <summary>Handles the Closing event of the Window control.</summary>
      /// <param name="sender">The source of the event.</param>
      /// <param name="e">The <see cref="System.ComponentModel.CancelEventArgs"/> instance containing the event data.</param>
      private void OnWindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
      {
         Properties.Settings.Default.Save();
         Properties.Colors.Default.Save();

         if (_host != null)
         {
            _host.IsClosing = true;
            _host.SetShouldExit(0);
         }
      }

      /// <summary>
      /// Handles the Deactivated event of the Window control.
      /// </summary>
      /// <param name="sender">The source of the event.</param>
      /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
      private void OnWindowDeactivated(object sender, EventArgs e)
      {
         if ((_host == null || !_host.IsClosing) && Properties.Settings.Default.AutoHide)
         {
            HideWindow();
         }
      }

      /// <summary>
      /// Handles the Loaded event of the Window control.
      /// </summary>
      /// <param name="sender">The source of the event.</param>
      /// <param name="e">The <see cref="System.Windows.RoutedEventArgs"/> instance containing the event data.</param>
      private void OnWindowLoaded(object sender, RoutedEventArgs e)
      {
         //buffer.Document.IsColumnWidthFlexible = false;
         Binding statusBinding = new Binding("StatusText");
         try
         {
            _host = new PoshHost((IPSUI)this);
            // // This doesn't actually solve the COM RCW problem
            // Dispatcher.Invoke((Action)(() => { _host.MakeConsole(); }));
            statusBinding.Source = _host.Options;
         }
         catch (Exception ex)
         {
            MessageBox.Show("Can't create PowerShell interface, are you sure PowerShell is installed? \n" + ex.Message + "\nAt:\n" + ex.Source, "Error Starting PoshConsole", MessageBoxButton.OK, MessageBoxImage.Stop);
            Application.Current.Shutdown(1);
         }

         // StatusBarItems:: Title, Separator, Admin, Separator, Status
         StatusBarItem el = status.Items[status.Items.Count - 1] as StatusBarItem;
         if (el != null)
         {
            el.SetBinding(StatusBarItem.ContentProperty, statusBinding);
         }

         if (PoshConsole.Interop.NativeMethods.IsUserAnAdmin())
         {
            // StatusBarItems:: Title, Separator, Admin, Separator, Status
            el = status.Items[2] as StatusBarItem;
            if (el != null)
            {
               el.Content = "Elevated!";
               el.Foreground = new SolidColorBrush(Color.FromRgb((byte)204, (byte)119, (byte)17));
               el.ToolTip = "PoshConsole is running as Administrator";
               el.Cursor = this.Cursor;
            }
         }
         Cursor = Cursors.IBeam;
      }

      /// <summary>Handles the LocationChanged event of the Window control.</summary>
      /// <param name="sender">The source of the event.</param>
      /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
      private void OnWindowLocationChanged(object sender, EventArgs e)
      {
         //System.Windows.SystemParameters.VirtualScreenHeight
         if (Properties.Settings.Default.SnapToScreenEdge)
         {
            CornerRadius radi = new CornerRadius(5.0);

            Rect workarea = new Rect(SystemParameters.VirtualScreenLeft,
                                      SystemParameters.VirtualScreenTop,
                                      SystemParameters.VirtualScreenWidth,
                                      SystemParameters.VirtualScreenHeight);

            if (Properties.Settings.Default.SnapDistance > 0)
            {
               if (this.Left - workarea.Left < Properties.Settings.Default.SnapDistance) this.Left = workarea.Left;
               if (this.Top - workarea.Top < Properties.Settings.Default.SnapDistance) this.Top = workarea.Top;
               if (workarea.Right - this.RestoreBounds.Right < Properties.Settings.Default.SnapDistance) this.Left = workarea.Right - this.RestoreBounds.Width;
               if (workarea.Bottom - this.RestoreBounds.Bottom < Properties.Settings.Default.SnapDistance) this.Top = workarea.Bottom - this.RestoreBounds.Height;
            }

            if (this.Left <= workarea.Left)
            {
               radi.BottomLeft = 0.0;
               radi.TopLeft = 0.0;
               this.Left = workarea.Left;
            }
            if (this.Top <= workarea.Top)
            {
               radi.TopLeft = 0.0;
               radi.TopRight = 0.0;
               this.Top = workarea.Top;
            }
            if (this.RestoreBounds.Right >= workarea.Right)
            {
               radi.TopRight = 0.0;
               radi.BottomRight = 0.0;
               this.Left = workarea.Right - this.RestoreBounds.Width;
            }
            if (this.RestoreBounds.Bottom >= workarea.Bottom)
            {
               radi.BottomRight = 0.0;
               radi.BottomLeft = 0.0;
               this.Top = workarea.Bottom - this.RestoreBounds.Height;
            }

            border.CornerRadius = radi;
         }
         Properties.Settings.Default.WindowLeft = Left;
         Properties.Settings.Default.WindowTop = Top;
      }

      /// <summary>
      /// Handles the SizeChanged event of the Window control.
      /// </summary>
      /// <param name="sender">The source of the event.</param>
      /// <param name="e">The <see cref="System.Windows.SizeChangedEventArgs"/> instance containing the event data.</param>
      private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
      {
         // we only recalculate when something other than animation changes the window size
         double h = (double)this.GetAnimationBaseValue(HeightProperty);
         if (Properties.Settings.Default.WindowHeight != h)
         {
            Properties.Settings.Default.WindowHeight = h;
            Properties.Settings.Default.WindowWidth = (double)this.GetAnimationBaseValue(WidthProperty);
         }
      }

      /// <summary>
      /// Handles the SourceInitialized event of the Window control.
      /// </summary>
      /// <param name="sender">The source of the event.</param>
      /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
      private void OnWindowSourceInitialized(object sender, EventArgs e)
      {
         Cursor = Cursors.AppStarting;
         // make the whole window glassy with -1
         Huddled.Interop.Vista.Glass.ExtendGlassFrame(this, new Thickness(-1));

         // hook mousedown and call DragMove() to make the whole window a drag handle
         MouseButtonEventHandler mbeh = new MouseButtonEventHandler(DragHandler);
         progress.PreviewMouseLeftButtonDown += mbeh;
         border.PreviewMouseLeftButtonDown += mbeh;
         buffer.PreviewMouseLeftButtonDown += mbeh;

         // hkManager = new WPFHotkeyManager(this);
         // hkManager.HotkeyPressed += new WPFHotkeyManager.HotkeyPressedEvent(Hotkey_Pressed);
         // FocusKey = Properties.Settings.Default.FocusKey;

         //if(FocusKey == null)
         //{
         //    Properties.Settings.Default.FocusKey = new Hotkey(Modifiers.Win, Keys.Oemtilde);
         //}

         //// this shouldn't be needed, because we hooked the settings.change event earlier
         //if(FocusKey == null || FocusKey.Id == 0)
         //{
         //    hkManager.Register(FocusKey);
         //}
         buffer.Focus();
      }

      void SettingsPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
      {
         if (!buffer.Dispatcher.CheckAccess())
         {
            buffer.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new SettingsChangedDelegate(SettingsPropertyChanged), sender, new object[] { e });
            return;
         }

         switch (e.PropertyName)
         {
            case "ShowInTaskbar":
               {
                  this.ShowInTaskbar = Properties.Settings.Default.ShowInTaskbar;
               } break;
            case "StatusBar":
               {
                  status.Visibility = Properties.Settings.Default.StatusBar ? Visibility.Visible : Visibility.Collapsed;
               } break;
            case "WindowHeight":
               {
                  this.Height = Properties.Settings.Default.WindowHeight;
               } break;
            case "WindowLeft":
               {
                  this.Left = Properties.Settings.Default.WindowLeft;
               } break;
            case "WindowWidth":
               {
                  this.Width = Properties.Settings.Default.WindowWidth;
               } break;
            case "WindowTop":
               {
                  this.Top = Properties.Settings.Default.WindowTop;
               } break;
            case "Animate":
               {
                  // do nothing, this setting is checked for each animation.
               } break;
            case "AutoHide":
               {
                  // do nothing, this setting is checked for each hide event.
               } break;
            case "SnapToScreenEdge":
               {
                  // do nothing, this setting is checked for each move
               } break;
            case "SnapDistance":
               {
                  // do nothing, this setting is checked for each move
               } break;
            case "AlwaysOnTop":
               {
                  this.Topmost = Properties.Settings.Default.AlwaysOnTop;
               } break;
            case "Opacity":
               {
                  this.Opacity = Properties.Settings.Default.Opacity;
               } break;
            case "WindowStyle":
               {
                  ((IPSConsole)buffer).WriteWarningLine("Window Style change requires a restart to take effect");
                  //this.WindowStyle = Properties.Settings.Default.WindowStyle;
                  //this.Hide();
                  //this.AllowsTransparency = (Properties.Settings.Default.WindowStyle == WindowStyle.None);
                  //this.Show();
               } break;
            case "BorderColorTopLeft":
               {
                  if (border.BorderBrush is LinearGradientBrush)
                  {
                     ((LinearGradientBrush)border.BorderBrush).GradientStops[0].Color = Properties.Settings.Default.BorderColorTopLeft;
                  }
               } break;
            case "BorderColorBottomRight":
               {
                  if (border.BorderBrush is LinearGradientBrush)
                  {
                     ((LinearGradientBrush)border.BorderBrush).GradientStops[1].Color = Properties.Settings.Default.BorderColorBottomRight;
                  }
               } break;
            case "BorderThickness":
               {
                  border.BorderThickness = Properties.Settings.Default.BorderThickness;
               } break;
            case "FocusKey":
               {
                  //HotkeyService.SetFocusHotkey(this, Properties.Settings.Default.FocusKey);

                  //if (FocusKey != null && FocusKey.Id != 0) hkManager.Unregister(FocusKey);

                  //if (Properties.Settings.Default.FocusKey != null)
                  //{
                  //    FocusKey = Properties.Settings.Default.FocusKey;
                  //    hkManager.Register(FocusKey);
                  //}
               } break;
            default: break;
         }
      }

      //  Internal Methods (1)

      internal void DragHandler(object sender, MouseButtonEventArgs e)
      {

         if (e.Source is Border || e.Source is ProgressPanel || (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
         {
            DragMove();
            e.Handled = true;
         }
      }

      #endregion 


      private void OnBufferPreviewMouseWheel(object sender, MouseWheelEventArgs e)
      {
         if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
         {
            buffer.FontSize += (e.Delta > 0) ? 1 : -1;
            e.Handled = true;
         }
      }

      void OnHandleDecreaseZoom(object sender, ExecutedRoutedEventArgs e)
      {
         buffer.FontSize -= 1;
      }

      void OnHandleIncreaseZoom(object sender, ExecutedRoutedEventArgs e)
      {
         buffer.FontSize += 1;
      }

      void OnHandleSetZoom(object sender, ExecutedRoutedEventArgs e)
      {
         double zoom;
         if (e.Parameter is double)
         {
            zoom = (double)e.Parameter;
            buffer.FontSize = zoom * Properties.Settings.Default.FontSize;
         }
         else if (e.Parameter is string && double.TryParse(e.Parameter.ToString(), out zoom))
         {
            buffer.FontSize = zoom * Properties.Settings.Default.FontSize;
         }
      }





   }
}
