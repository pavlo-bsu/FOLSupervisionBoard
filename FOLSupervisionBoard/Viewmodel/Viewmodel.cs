using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.Metrics;
using System.IO.Ports;
using System.Linq;
using System.Security.Cryptography.Pkcs;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Pavlo.FOLSupervisionBoard.Viemodel;
using Pavlo.MyHelpers.MVVM;

namespace Pavlo.FOLSupervisionBoard
{
    /// <summary>
    /// VM of the main window
    /// </summary>
    public class Viewmodel:INPCBaseDotNet4_5
    {
        #region Commands
        private CommandRefresh _TheCommandRefresh;
        public CommandRefresh TheCommandRefresh
        {
            get
            {
                return _TheCommandRefresh;
            }
            set
            {
                _TheCommandRefresh = value;
            }
        }

        private CommandReset _TheCommandReset;
        public CommandReset TheCommandReset
        {
            get
            {
                return _TheCommandReset;
            }
            set
            {
                _TheCommandReset = value;
            }
        }
        #endregion

        #region COMport
        private ObservableCollection<string> _AvaliableCOMports = new ObservableCollection<string>();
        /// <summary>
        /// list of AvaliableCOMports
        /// </summary>
        public ObservableCollection<string> AvaliableCOMports
        {
            get { return _AvaliableCOMports; }
            set { _AvaliableCOMports = value; }
        }

        /// <summary>
        /// fill AvaliableCOMports
        /// </summary>
        private void FillAvaliableCOMports()
        {
            var tmpList = SerialPort.GetPortNames();
            AvaliableCOMports.Clear();
            foreach (var portName in tmpList)
            {
                AvaliableCOMports.Add(portName);
            }
        }

        private int _AvaliableCOMportsSelectedIndex = -1;
        /// <summary>
        /// index of the selected COMport in AvaliableCOMports
        /// </summary>
        public int AvaliableCOMportsSelectedIndex
        {
            set
            {
                if (value == _AvaliableCOMportsSelectedIndex)
                    return;

                _AvaliableCOMportsSelectedIndex = value;

                //Dispose previous connection
                TheMOT2000T?.Dispose();

                //run task to release UI (thus there is no Wait() for the task)
                Task.Run(async() =>
                {
                    //try to set the connection
                    TheMOT2000T = new MOT2000T(AvaliableCOMports[value]);
                    var res = TheMOT2000T.OpenPort();
                    if (res == true)
                    {//i.e. connection to COMport is established
                        await RefreshDataAsync();
                    }
                    else
                    {//i.e. connection to COMport is NOT established
                        string msg = "Сonnection cannot be established!";
                        MessageBox.Show(msg, "Warning", MessageBoxButton.OK, MessageBoxImage.Information);

                        //dirty hack for Cancelling Selection in a Bound WPF Combo Box
                        //https://stackoverflow.com/questions/2608071/wpf-cancel-a-user-selection-in-a-databound-listbox/7556834#7556834
                        await Application.Current.Dispatcher.BeginInvoke(
                            new Action(() =>
                            {
                                _AvaliableCOMportsSelectedIndex = -1;
                                NotifyPropertyChanged(nameof(AvaliableCOMportsSelectedIndex));
                            }),
                            DispatcherPriority.ContextIdle,
                            null
                        );
                        
                        //and finally reset all UI
                        await ResetAllUIAsync();
                    }
                });
            }
            get
            {
                return _AvaliableCOMportsSelectedIndex;
            }
        }
        #endregion

        #region RX, TX voltage and opt.link level   
        private string _RXsn = MOT2000T.defaultName;
        /// <summary>
        /// receiver serial number
        /// </summary>
        public string RXsn
        {
            get => _RXsn;
            set
            {
                _RXsn = value;
                NotifyPropertyChanged();
            }
        }

        /// <summary>
        /// Request RX Identity
        /// </summary>
        /// <param name="handleException">handle exception, i.e. show message</param>
        /// <returns></returns>
        public async Task RequestRXIdentityAsync(bool handleException)
        {
            await Application.Current.Dispatcher.BeginInvoke(
                            new Action(() =>
                            {
                                IsResposeAwaiting = true;
                            }),
                            DispatcherPriority.ContextIdle,
                            null
                            );
            try
            {
                Task<string> t = TheMOT2000T.RequestRXIdentityAsync();
                await t;
                var rs = t.Result;
                await Application.Current.Dispatcher.BeginInvoke(
                           new Action(() =>
                           {
                               this.RXsn = rs;
                           }),
                           DispatcherPriority.ContextIdle,
                           null
                           );
            }
            catch (Exception e)
            {
                if (handleException)
                {
                    string msg = e.Message;
                    MessageBox.Show(msg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    throw;
                }
            }
            finally
            {
                await Application.Current.Dispatcher.BeginInvoke(
                           new Action(() =>
                           {
                               IsResposeAwaiting = false;
                           }),
                           DispatcherPriority.ContextIdle,
                           null
                           );
            }
        }


        private double _RXbatteryVoltage = MOT2000T.defaultDoubleValue;
        /// <summary>
        /// receiver battery voltage
        /// </summary>
        public double RXbatteryVoltage
        {
            get => _RXbatteryVoltage;
            set
            {
                _RXbatteryVoltage = value;
                NotifyPropertyChanged();
            }
        }

        /// <summary>
        /// Request RX battery voltage
        /// </summary>
        /// <param name="handleException">handle exception, i.e. show message</param>
        /// <returns></returns>
        public async Task RequestRXBatteryVoltageAsync(bool handleException)
        {
            await Application.Current.Dispatcher.BeginInvoke(
                            new Action(() =>
                            {
                                IsResposeAwaiting = true;
                            }),
                            DispatcherPriority.ContextIdle,
                            null
                            );
            try
            {
                Task<double> t = TheMOT2000T.RequestRXBatteryVoltageAsync();
                await t;
                var rs = t.Result;
                await Application.Current.Dispatcher.BeginInvoke(
                            new Action(() =>
                            {
                                this.RXbatteryVoltage = rs;
                            }),
                            DispatcherPriority.ContextIdle,
                            null
                            );
            }
            catch (Exception e)
            {
                if (handleException)
                {
                    string msg = e.Message;
                    MessageBox.Show(msg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    throw;
                }
            }
            finally
            {
                await Application.Current.Dispatcher.BeginInvoke(
                            new Action(() =>
                            {
                                IsResposeAwaiting = false;

                            }),
                            DispatcherPriority.ContextIdle,
                            null
                            );
            }
        }

        private string _TXsn = MOT2000T.defaultName;
        /// <summary>
        /// transmitter serial number
        /// </summary>
        public string TXsn
        {
            get => _TXsn;
            set
            {
                _TXsn = value;
                NotifyPropertyChanged();
                if (value != MOT2000T.defaultName)
                {
                    NotifyPropertyChanged_AvailabilityOfElements();
                }
            }
        }

        /// <summary>
        /// Request TX Identity
        /// </summary>
        /// <param name="handleException">handle exception, i.e. show message</param>
        /// <returns>true if TX was connected first time (i.e. new TX identity after not connected TX state)</returns>
        public async Task<bool> RequestTXIdentityAsync(bool handleException)
        {
            bool isTXfirstTimeConnected = false;

            await Application.Current.Dispatcher.BeginInvoke(
                            new Action(() =>
                            {
                                IsResposeAwaiting = true;
                            }),
                            DispatcherPriority.ContextIdle,
                            null
                            );
            try
            {
                Task<string> t = TheMOT2000T.RequestTXIdentityAsync();
                await t;
                var ts = t.Result;

                if (TXsn == MOT2000T.defaultName && ts != MOT2000T.defaultName)
                {//TX was connected first time(i.e. new TX identity after not connected TX state)
                    isTXfirstTimeConnected = true;
                }

                //set new value
                await Application.Current.Dispatcher.BeginInvoke(
                           new Action(() =>
                           {
                               this.TXsn = ts;
                           }),
                           DispatcherPriority.ContextIdle,
                           null
                           );
            }
            catch (Exception e)
            {
                if (handleException)
                {
                    string msg = e.Message;
                    MessageBox.Show(msg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    throw;
                }
            }
            finally
            {
                await Application.Current.Dispatcher.BeginInvoke(
                           new Action(() =>
                           {
                               IsResposeAwaiting = false;
                           }),
                           DispatcherPriority.ContextIdle,
                           null
                           );
            }
            return isTXfirstTimeConnected;
        }

        
        private double _TXbatteryVoltage = MOT2000T.defaultDoubleValue;
        /// <summary>
        /// transmitter battery voltage
        /// </summary>
        public double TXbatteryVoltage
        {
            get => _TXbatteryVoltage;
            set
            {
                _TXbatteryVoltage = value;
                NotifyPropertyChanged();
            }
        }

        /// <summary>
        /// Request TX battery voltage
        /// </summary>
        /// <param name="handleException">handle exception, i.e. show message</param>
        /// <returns></returns>
        public async Task RequestTXBatteryVoltageAsync(bool handleException)
        {
            await Application.Current.Dispatcher.BeginInvoke(
                            new Action(() =>
                            {
                                IsResposeAwaiting = true;
                            }),
                            DispatcherPriority.ContextIdle,
                            null
                            );
            try
            {
                Task<double> t = TheMOT2000T.RequestTXBatteryVoltageAsync();
                await t;
                var txv = t.Result;
                await Application.Current.Dispatcher.BeginInvoke(
                            new Action(() =>
                            {
                                this.TXbatteryVoltage = txv;
                            }),
                            DispatcherPriority.ContextIdle,
                            null
                            );
            }
            catch (Exception e)
            {
                if (handleException)
                {
                    string msg = e.Message;
                    MessageBox.Show(msg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    throw;
                }
            }
            finally
            {
                await Application.Current.Dispatcher.BeginInvoke(
                            new Action(() =>
                            {
                                IsResposeAwaiting = false;

                            }),
                            DispatcherPriority.ContextIdle,
                            null
                            );
            }
        }

        
        private double _OptSignalLvl = MOT2000T.defaultDoubleValue;
        /// <summary>
        /// opt. signal level
        /// </summary>
        public double OptSignalLvl
        {
            get => _OptSignalLvl;
            set
            {
                _OptSignalLvl = value;
                NotifyPropertyChanged();
            }
        }

        /// <summary>
        /// Request opt. link level
        /// </summary>
        /// <param name="handleException">handle exception, i.e. show message</param>
        /// <returns></returns>
        public async Task RequestOptLinkLvlAsync(bool handleException)
        {
            await Application.Current.Dispatcher.BeginInvoke(
                            new Action(() =>
                            {
                                IsResposeAwaiting = true;
                            }),
                            DispatcherPriority.ContextIdle,
                            null
                            );
            try
            {
                Task<double> t = TheMOT2000T.RequestOptLinkLvlAsync();
                await t;
                var lvl = t.Result;
                await Application.Current.Dispatcher.BeginInvoke(
                            new Action(() =>
                            {
                                this.OptSignalLvl = lvl;
                            }),
                            DispatcherPriority.ContextIdle,
                            null
                            );
            }
            catch (Exception e)
            {
                if (handleException)
                {
                    string msg = e.Message;
                    MessageBox.Show(msg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    throw;
                }
            }
            finally
            {
                await Application.Current.Dispatcher.BeginInvoke(
                            new Action(() =>
                            {
                                IsResposeAwaiting = false;

                            }),
                            DispatcherPriority.ContextIdle,
                            null
                            );
            }
        }
        #endregion

        #region availability of UI elements
        private bool _IsResposeAwaiting = false;
        /// <summary>
        /// Was request send and is response awaiting
        /// </summary>
        public bool IsResposeAwaiting
        {
            get => _IsResposeAwaiting;
            set
            {
                _IsResposeAwaiting = value;
                NotifyPropertyChanged_AvailabilityOfElements();
            }
        }

        /// <summary>
        /// indicates whether COM-port can be selected in the combobox
        /// </summary>
        public bool CanCOMportBeSelected
        {
            get
            {
                if (this.AvaliableCOMportsSelectedIndex < 0)
                {//i.e. com-port is still unselected => no requests running
                    return true;
                }
                else
                {//i.e. com-port is selected
                    if (IsResposeAwaiting)
                    {
                        return false;
                    }
                    else
                    {
                        if (!AreUnitsOnLowpowerMode && !IsTestGenerOn)
                        {
                            return true;
                        }

                        return false;
                    }
                }
            }
        }

        /// <summary>
        /// indicates whether TX was connected via COM-port
        /// </summary>
        public bool IsTXConnected
        {
            get
            {
                if (this.TheMOT2000T != null)
                {
                    if (TXsn != MOT2000T.defaultName)
                        return true;
                    else
                        return false;
                }
                else return false;
            }
        }

        /// <summary>
        /// indicates whether gain can be selected in the combobox
        /// </summary>
        public bool CanGainBeSelected
        {
            get
            {
                if (IsResposeAwaiting)
                {
                    return false;
                }
                else
                {
                    if (IsTXConnected && !AreUnitsOnLowpowerMode && !IsTestGenerOn)
                    {
                        return true;
                    }

                    return false;
                }
            }
        }

        /// <summary>
        /// indicates whether test generator can be switched
        /// </summary>
        public bool CanTestGeneratorBeSwitched
        {
            get
            {
                if (IsResposeAwaiting)
                {
                    return false;
                }
                else
                {
                    if (IsTXConnected && !AreUnitsOnLowpowerMode)
                    {
                        return true;
                    }

                    return false;
                }
            }
        }

        /// <summary>
        /// indicates whether lowpower mode of RX and TX can be switched 
        /// </summary>
        public bool CanLowpowerModeOfUnitsBeSwitched
        {
            get
            {
                if (IsResposeAwaiting)
                {
                    return false;
                }
                else
                {
                    if (IsTXConnected && !IsTestGenerOn)
                    {
                        return true;
                    }

                    return false;
                }
            }
        }

        /// <summary>
        /// rise NPC for properties related to availability of main controls of the window 
        /// </summary>
        private void NotifyPropertyChanged_AvailabilityOfElements()
        {
            NotifyPropertyChanged(nameof(CanCOMportBeSelected));
            NotifyPropertyChanged(nameof(IsResposeAwaiting));
            NotifyPropertyChanged(nameof(IsTXConnected));
            NotifyPropertyChanged(nameof(CanGainBeSelected));
            NotifyPropertyChanged(nameof(CanTestGeneratorBeSwitched));
            NotifyPropertyChanged(nameof(CanLowpowerModeOfUnitsBeSwitched));
        }
    #endregion

        private MOT2000T _TheMOT2000T;
        /// <summary>
        /// represents Montena (c) MOT2000T (model level)
        /// </summary>
        public MOT2000T TheMOT2000T
        {
            get => _TheMOT2000T;
            set { _TheMOT2000T = value; }
        }

        public Viewmodel()
        {
            TheCommandRefresh = new CommandRefresh(this);
            TheCommandReset = new CommandReset(this);
            FillAvaliableCOMports();
            FillAvaliableGains();
        }

        /// <summary>
        /// Set Name and Voltage of RX, TX, and opt. link level to the default values
        /// </summary>
        /// <returns></returns>
        private async Task ResetRX_TX_toDefaultValuesAsync()
        {
            await Application.Current.Dispatcher.BeginInvoke(
                           new Action(() =>
                           {
                               this.RXsn = MOT2000T.defaultName;
                               this.RXbatteryVoltage = MOT2000T.defaultDoubleValue;
                               this.TXsn = MOT2000T.defaultName;
                               this.TXbatteryVoltage = MOT2000T.defaultDoubleValue;
                               this.OptSignalLvl = MOT2000T.defaultDoubleValue;

                           }),
                           DispatcherPriority.ContextIdle,
                           null
                           );
        }

        /// <summary>
        /// Set all UI elemenets to default values
        /// </summary>
        /// <returns></returns>
        private async Task ResetAllUIAsync()
        {
            await ResetRX_TX_toDefaultValuesAsync();
            await Application.Current.Dispatcher.BeginInvoke(
                           new Action(() =>
                           {
                               //changing only view. Not rising request
                               _GainsListSelectedIndex = -1;
                               NotifyPropertyChanged(nameof(GainsListSelectedIndex));

                               //changing only view. Not rising request
                               _AreUnitsOnLowpowerMode = false;
                               NotifyPropertyChanged(nameof(AreUnitsOnLowpowerMode));
                               
                               //changing only view. Not rising request
                               _IsTestGenerOn = false;
                               NotifyPropertyChanged(nameof(IsTestGenerOn));
                               
                               NotifyPropertyChanged_AvailabilityOfElements();
                           }),
                           DispatcherPriority.ContextIdle,
                           null
                           );
        }

        /// <summary>
        /// Refresh Name and Voltage for RX, TX, and opt. link level.
        /// </summary>
        public async Task RefreshDataAsync()
        {
            try
            {
                //firstly reset to default values
                await ResetRX_TX_toDefaultValuesAsync();

                //for receiver
                var t1 = RequestRXIdentityAsync(false);
                await t1;
                var t2 = RequestRXBatteryVoltageAsync(false);
                await t2;

                //for transmitter
                var t3 = RequestTXIdentityAsync(false);
                await t3;
                if (t3.Result==true)
                {//i.e. TX was connected first time (new TX identity after state "TX not connected")
                    var tRequestAttTask = RequestGainAsync();
                    await tRequestAttTask;
                }
                var t4 = RequestTXBatteryVoltageAsync(false);
                await t4;

                //for opt.link level
                var tLvl = RequestOptLinkLvlAsync(false);
                await tLvl;
            }
            catch (Exception e)
            {
                string msg = e.Message;
                MessageBox.Show(msg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Do the work during window closing
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            //Dispose connection to the device
            TheMOT2000T?.Dispose();
        }

        #region gain
        private ObservableCollection<string> _GainsList = new ObservableCollection<string>();
        /// <summary>
        /// Avaliable values of gain
        /// </summary>
        public ObservableCollection<string> GainsList
        {
            get { return _GainsList; }
            set { _GainsList = value; }
        }

        /// <summary>
        /// fill gain values in the range 0dB ... 31.75dB with a step of 0.25dB
        /// </summary>
        private void FillAvaliableGains()
        {
            GainsList.Clear();
            for (int i = 0; i <= 31; i++)
            {
                GainsList.Add(i.ToString() + ".00");
                GainsList.Add(i.ToString() + ".25");
                GainsList.Add(i.ToString() + ".50");
                GainsList.Add(i.ToString() + ".75");
            }
        }

        private int _GainsListSelectedIndex = -1;
        /// <summary>
        /// index of the selected gain in GainsList
        /// </summary>
        public int GainsListSelectedIndex
        {
            set
            {
                if (value == _GainsListSelectedIndex)
                    return;

                _GainsListSelectedIndex = value;
                NotifyPropertyChanged();

                //run task to release UI (thus there is no Wait() for the task)
                Task.Run(async () =>
                {
                    await Application.Current.Dispatcher.BeginInvoke(
                            new Action(() =>
                            {
                                IsResposeAwaiting = true;
                            }),
                            DispatcherPriority.ContextIdle,
                            null
                            );
                    
                    bool error=false;
                    try
                    {
                        if (value != -1)
                        {
                            await TheMOT2000T.SetAttenuationAsync(Double.Parse(GainsList[_GainsListSelectedIndex]));
                        }
                    }
                    catch (Exception e)
                    {
                        error = true;
                        string msg = $"Error was occurred during the operation:\r\n{e.Message}\r\n---\r\nFiber optic link will be reset!\r\n";
                        MessageBox.Show(msg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    finally
                    {
                        await Application.Current.Dispatcher.BeginInvoke(
                                new Action(() =>
                                {
                                    IsResposeAwaiting = false;
                                }),
                                DispatcherPriority.ContextIdle,
                                null
                                );
                        
                        if (error)
                            ResetAsync();//Fiber optic link should be reset!
                    }
                });
            }
            get
            {
                return _GainsListSelectedIndex;
            }
        }

        /// <summary>
        /// Request gain (attenuation)
        /// </summary>
        /// <returns></returns>
        public async Task RequestGainAsync()
        {
            await Application.Current.Dispatcher.BeginInvoke(
                            new Action(() =>
                            {
                                IsResposeAwaiting = true;
                            }),
                            DispatcherPriority.ContextIdle,
                            null
                            );
            bool error = false;
            try
            {
                Task<double> t = TheMOT2000T.RequestAllSetupAsync();
                await t;
                var gain = t.Result;
                //index of gain in the GainsList
                var gainIndex = GainsList.IndexOf(gain.ToString("F2"));
                await Application.Current.Dispatcher.BeginInvoke(
                            new Action(() =>
                            {
                                this._GainsListSelectedIndex = gainIndex;
                                NotifyPropertyChanged(nameof(GainsListSelectedIndex));
                            }),
                            DispatcherPriority.ContextIdle,
                            null
                            );
            }
            catch (Exception e)
            {
                error = true;
                string msg = $"Error was occurred during the operation:\r\n{e.Message}\r\n---\r\nFiber optic link will be reset!\r\n";
                MessageBox.Show(msg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                await Application.Current.Dispatcher.BeginInvoke(
                            new Action(() =>
                            {
                                IsResposeAwaiting = false;

                            }),
                            DispatcherPriority.ContextIdle,
                            null
                            );
                if (error)
                    ResetAsync();//Fiber optic link should be reset!
            }
        }
        #endregion

        #region reset opt system
        /// <summary>
        /// Reset opt.system and update UI
        /// </summary>
        public async Task ResetAsync()
        {
            try
            {
                await SendResetCmdAsync();
            }
            catch (Exception e)
            {
                string msg = $"It looks like something went wrong during the reset.\r\nError: {e.Message}\r\n---\r\nCheck all connections and do hard reset of RX and TX.";
                Task.Run(() =>
                {
                    MessageBox.Show(msg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
            finally
            {
                //in any case UI should be reseted
                await ResetAllUIAsync();

                //then refreshed
                await RefreshDataAsync();
            }
        }

        /// <summary>
        /// send reset command to the device
        /// </summary>
        private async Task SendResetCmdAsync()
        {
            await Application.Current.Dispatcher.BeginInvoke(
                            new Action(() =>
                            {
                                IsResposeAwaiting = true;
                            }),
                            DispatcherPriority.ContextIdle,
                            null
                            );
            try
            {
                await TheMOT2000T.SendResetCmdAsync();
            }
            finally
            {
                await Application.Current.Dispatcher.BeginInvoke(
                           new Action(() =>
                           {
                               IsResposeAwaiting = false;
                           }),
                           DispatcherPriority.ContextIdle,
                           null
                           );
            }
        }
        #endregion

        #region testGenerator
        /// <summary>
        /// set test signal generator to ON or OFF
        /// </summary>
        /// <param name="onVal">is set to ON</param>
        /// <returns></returns>
        private async Task SetTestGeneratorAsync(bool onVal)
        {
            await Application.Current.Dispatcher.BeginInvoke(
                            new Action(() =>
                            {
                                IsResposeAwaiting = true;
                            }),
                            DispatcherPriority.ContextIdle,
                            null
                            );
            try
            {
                await TheMOT2000T.SetTestGeneratorAsync(onVal);
            }
            finally
            {
                await Application.Current.Dispatcher.BeginInvoke(
                           new Action(() =>
                           {
                               IsResposeAwaiting = false;
                           }),
                           DispatcherPriority.ContextIdle,
                           null
                           );
            }
        }

        private bool _IsTestGenerOn = false;
        /// <summary>
        /// Is test generator ON
        /// </summary>
        public bool IsTestGenerOn
        {
            get
            {
                return _IsTestGenerOn;
            }
            set
            {
                if (_IsTestGenerOn != value)
                {
                    _IsTestGenerOn = value;
                    NotifyPropertyChanged();
                    
                    //run task to release UI (thus there is no Wait() for the task)
                    Task.Run(async () =>
                    {
                        bool error=false;
                        try
                        {
                            await SetTestGeneratorAsync(value);
                        }
                        catch (Exception e)
                        {
                            error = true;
                            string msg = $"Error was occurred during the operation:\r\n{e.Message}\r\n---\r\nFiber optic link will be reset!\r\n";
                            MessageBox.Show(msg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                        finally
                        {
                            await Application.Current.Dispatcher.BeginInvoke(
                               new Action(() =>
                               {
                                   NotifyPropertyChanged_AvailabilityOfElements();
                               }),
                               DispatcherPriority.ContextIdle,
                               null
                               );

                            if (error)
                                ResetAsync();//Fiber optic link should be reset!
                        }
                    });
                }
            }
        }
        #endregion

        #region Standby / LowPowerMode
        /// <summary>
        /// set RX/TX to low/normal power mode
        /// </summary>
        /// <param name="isLowPowerMode">true - low power mode, false - normal power mode</param>
        /// <returns></returns>
        private async Task SetUnitsToLowpowerModeAsync(bool isLowPowerMode)
        {
            await Application.Current.Dispatcher.BeginInvoke(
                            new Action(() =>
                            {
                                IsResposeAwaiting = true;
                            }),
                            DispatcherPriority.ContextIdle,
                            null
                            );
            try
            {
                //set TX
                await TheMOT2000T.SetUnitToLowPowerAsync(false, isLowPowerMode);
                //set RX
                await TheMOT2000T.SetUnitToLowPowerAsync(true, isLowPowerMode);
            }
            finally
            {
                await Application.Current.Dispatcher.BeginInvoke(
                           new Action(() =>
                           {
                               IsResposeAwaiting = false;
                           }),
                           DispatcherPriority.ContextIdle,
                           null
                           );
            }
        }

        
        private bool _AreUnitsOnLowpowerMode = false;
        /// <summary>
        /// Are RX and TX on low power mode 
        /// </summary>
        public bool AreUnitsOnLowpowerMode
        {
            get
            {
                return _AreUnitsOnLowpowerMode;
            }
            set
            {
                if (_AreUnitsOnLowpowerMode != value)
                {
                    _AreUnitsOnLowpowerMode = value;
                    NotifyPropertyChanged(nameof(AreUnitsOnLowpowerMode));

                    //run task to release UI (thus there is no Wait() for the task)
                    Task.Run(async () =>
                    {
                        bool error=false;
                        try
                        {
                            await SetUnitsToLowpowerModeAsync(value);
                        }
                        catch (Exception e)
                        {
                            error = true;
                            string msg = $"Error was occurred during the operation:\r\n{e.Message}\r\n---\r\nFiber optic link will be reset!\r\n";
                            MessageBox.Show(msg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                        finally
                        {
                            await Application.Current.Dispatcher.BeginInvoke(
                               new Action(() =>
                               {
                                   NotifyPropertyChanged_AvailabilityOfElements();
                               }),
                               DispatcherPriority.ContextIdle,
                               null
                               );

                            if (error)
                                ResetAsync();//Fiber optic link should be reset!
                        }
                    });
                }
            }
        }
        #endregion
    }
}
