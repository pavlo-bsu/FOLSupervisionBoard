using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Ports;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Runtime.CompilerServices;
using System.Windows.Media.Animation;

namespace Pavlo.FOLSupervisionBoard
{
    /// <summary>
    /// class for Montena (c) MOT2000T. Model level
    /// </summary>
    public class MOT2000T:IDisposable
    {
        #region COMport
        private SerialPort _TheSerialPort;

        public SerialPort TheSerialPort
        {
            get => _TheSerialPort;
            set { _TheSerialPort = value; }
        }

        private string _COMportName = string.Empty;
        public string COMportName
        {
            get
            {
                return _COMportName;
            }
            protected set
            { _COMportName = value; }
        }

        private int _BaudRate = 0;
        public int BaudRate
        {
            get { return _BaudRate; }
            set { _BaudRate = value; }
        }

        private Parity _TheParity = Parity.None;
        public Parity TheParity
        {
            get { return _TheParity; }
            set { _TheParity = value; }
        }

        private int _DataBits = -1;
        public int DataBits
        {
            get { return _DataBits; }
            set { _DataBits = value; }
        }

        private StopBits _StopBits = StopBits.None;
        public StopBits StopBits
        {
            get { return _StopBits; }
            set { _StopBits = value; }
        }

        private int _TimeOut = -1;
        /// <summary>
        /// value of timeout for each request to the device.
        /// [ms]
        /// </summary>
        private int TimeOut
        {
            get => _TimeOut;
            set { _TimeOut = value; }
        }

        private int _SleepTime = -1;
        /// <summary>
        /// time to thread sleep before reading the data from COMport.
        /// Some kind of dirty hack to ensure that all kind of unxpected data is recieved during single read of the data.
        /// [ms]
        /// </summary>
        private int SleepTime
        {
            get => _SleepTime;
            set { _SleepTime = value; }
        }

        /// <summary>
        /// close COM port
        /// </summary>
        public void ClosePort()
        {
            TheSerialPort?.Close();
        }
        #endregion

        private string _BadResponce = string.Empty;
        /// <summary>
        /// typical responce of reciever for command, if there is something wrong
        /// </summary>
        private string BadResponce
        {
            get => _BadResponce
                ;
            set { _BadResponce = value; }
        }

        /// <summary>
        /// default name for RX and TX (to be displayed in UI)
        /// </summary>
        public static string defaultName
        { get => string.Empty; }

        /// <summary>
        /// default value of double variables, like battery voltage, signal level, etc. (to be displayed in UI)
        /// </summary>
        public static double defaultDoubleValue
        { get => 0d; }

        /// <summary>
        /// Is connection COMport-RX estableshed
        /// </summary>
        private bool _IsConnectionEstableshed=false;
        public bool IsConnectionEstableshed
        {
            get => _IsConnectionEstableshed;
            set { _IsConnectionEstableshed = value; }
        }

        /// <summary>
        /// receiver Identity: manufacturer, type, serial number
        /// </summary>
        private string _RXsn = string.Empty;
        public string RXsn
        {
            get => _RXsn;
            set { _RXsn = value.Trim(); }
        }   

        /// <summary>
        /// Battery voltage of receiver
        /// </summary>
        private double _RXbattVoltage = 0d;
        public double RXbattVoltage
        {
            get => _RXbattVoltage;
            set { _RXbattVoltage = value; }
        }

        /// <summary>
        /// transmitter: manufacturer, type, serial number
        /// </summary>
        private string _TXsn = string.Empty;
        public string TXsn
        {
            get => _TXsn;
            set { _TXsn = value.Trim(); }
        }

        /// <summary>
        /// Battery voltage of transmitter
        /// </summary>
        private double _TXbattVoltage = 0d;
        public double TXbattVoltage
        {
            get => _TXbattVoltage;
            set { _TXbattVoltage = value; }
        }

        /// <summary>
        /// Optical link level
        /// </summary>
        private double _OptLinkLvl = 0d;
        public double OptLinkLvl
        {
            get => _OptLinkLvl;
            set { _OptLinkLvl = value; }
        }

        public MOT2000T()
        {
            //set values according to Montena UM
            BaudRate = 9600;
            TheParity = Parity.None;
            DataBits = 8;
            StopBits = StopBits.One;

            //timeout
            TimeOut = 5000;
            SleepTime = 100;

            BadResponce = "NACK";
        }
        public MOT2000T(string comPort) : this()
        {
            this.COMportName = comPort;
        }

        #region IDisposable by Microsoft recommendation
        private bool disposed = false;

        // realization of IDisposable.
        public void Dispose()
        {
            Dispose(true);
            // suppress finilization
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed) return;
            if (disposing)
            {
                //dispose managed resources
                //serialport is a managed resource, which itself owns an unmanaged resource 
                //see https://stackoverflow.com/questions/4826702/is-serialport-in-net-unmanaged-resource-is-my-wrapped-class-correct
                TheSerialPort?.Close();
                TheSerialPort?.Dispose();
            }
            //dispose unmanaged resources
            //there is no unmanaged resources

            disposed = true;
        }

        // Деструктор
        ~MOT2000T()
        {
            Dispose(false);
        }
        #endregion

        /// <summary>
        /// open new serial port connection with RX of FOC 
        /// </summary>
        /// <returns>true - connection estableshed</returns>
        public bool OpenPort()
        {
            try
            {
                TheSerialPort = new SerialPort(COMportName, BaudRate, TheParity, DataBits, StopBits);
                TheSerialPort.NewLine = "\n";
                TheSerialPort.Open();
                IsConnectionEstableshed = true;
            }
            catch
            {
                IsConnectionEstableshed = false;
            }
            return IsConnectionEstableshed;
        }

        #region requests
        /// <summary>
        /// request RX Identity (RXsn)
        /// </summary>
        /// <returns>RX Identity (RXsn)</returns>
        public async Task<string> RequestRXIdentity()
        {
            TaskCompletionSource<string> tcs = new TaskCompletionSource<string>();
            
            //check presets
            if (TheSerialPort == null || !IsConnectionEstableshed)
            {
                tcs.SetException(new NullReferenceException("The serial port is not ready!"));
                await tcs.Task;
                //return await tcs.Task;  <- code will never reach "return" because the exception will rise before
            }

            //for cancellation of the Task after timeout
            CancellationTokenSource ct = new CancellationTokenSource();
            ct.CancelAfter(this.TimeOut);

            //handler for the device responce
            SerialDataReceivedEventHandler handler = (object sender, SerialDataReceivedEventArgs e) =>
            {
                Thread.Sleep(SleepTime);
                string str = string.Empty;
                if (TheSerialPort.BytesToRead > 0)
                    str = TheSerialPort.ReadExisting();

                if (str.StartsWith(this.BadResponce))
                {
                    this.RXsn = defaultName;
                }
                else
                {
                    this.RXsn = str;
                }
                tcs.SetResult(this.RXsn);
            };

            //subscribe to device responce
            TheSerialPort.DataReceived += handler;

            try 
            {
                using (ct.Token.Register(() => tcs.SetCanceled(), false))//for cancellation
                {
                    TheSerialPort.WriteLine(":IDR?");
                    return await tcs.Task;
                }
            }
            finally
            {
                //unsubscribe
                TheSerialPort.DataReceived -= handler;
            }

        }

        /// <summary>
        /// request RX battery voltage
        /// </summary>
        /// <returns>RX battery voltage</returns>
        public async Task<double> RequestRXBatteryVoltage()
        {
            TaskCompletionSource<double> tcs = new TaskCompletionSource<double>();

            //check presets
            if (TheSerialPort == null || !IsConnectionEstableshed)
            {
                tcs.SetException(new NullReferenceException("The serial port is not ready!"));
                return await tcs.Task;
            }

            //for cancellation of the Task after timeout
            CancellationTokenSource ct = new CancellationTokenSource();
            ct.CancelAfter(this.TimeOut);

            //handler for the device responce
            SerialDataReceivedEventHandler handler = (object sender, SerialDataReceivedEventArgs e) =>
            {
                Thread.Sleep(SleepTime);
                string str = string.Empty;
                if (TheSerialPort.BytesToRead > 0)
                    str = TheSerialPort.ReadExisting();
                double voltage;
                var val = Double.TryParse(str, out voltage);
                if (val)
                {
                    tcs.SetResult(voltage);
                    this.RXbattVoltage = voltage;
                }
                else
                {
                    if (str.StartsWith(this.BadResponce))
                        this.RXbattVoltage = defaultDoubleValue;
                    else
                    {
                        if (!tcs.Task.IsCompleted)
                        {
                            tcs.SetException(new ArgumentException("Unacceptable RX voltage value!"));
                        }
                    }
                }
            };

            //subscribe to device responce
            TheSerialPort.DataReceived += handler;

            try
            {
                using (ct.Token.Register(() => tcs.SetCanceled(), false))//for cancellation
                {
                    TheSerialPort.WriteLine(":BATR?");
                    return await tcs.Task;
                }
            }
            finally
            {
                //unsubscribe
                TheSerialPort.DataReceived -= handler;
            }
        }

        /// <summary>
        /// request TX Identity (TXsn)
        /// </summary>
        /// <returns>TX Identity (TXsn)</returns>
        public async Task<string> RequestTXIdentity()
        {
            TaskCompletionSource<string> tcs = new TaskCompletionSource<string>();

            //check presets
            if (TheSerialPort == null || !IsConnectionEstableshed)
            {
                tcs.SetException(new NullReferenceException("The serial port is not ready!"));
                return await tcs.Task;
            }

            //for cancellation of the Task after timeout
            CancellationTokenSource ct = new CancellationTokenSource();
            ct.CancelAfter(this.TimeOut);

            //handler for the device responce
            SerialDataReceivedEventHandler handler = (object sender, SerialDataReceivedEventArgs e) =>
            {
                Thread.Sleep(SleepTime);
                string str = string.Empty;
                if (TheSerialPort.BytesToRead > 0)
                    str = TheSerialPort.ReadExisting();
                if (str.StartsWith(this.BadResponce))
                {
                    this.TXsn = defaultName;
                }
                else
                {
                    this.TXsn = str;
                }
                tcs.SetResult(this.TXsn);
            };

            //subscribe to device responce
            TheSerialPort.DataReceived += handler;

            try
            {
                using (ct.Token.Register(() => tcs.SetCanceled(), false))//for cancellation
                {
                    TheSerialPort.WriteLine(":IDT?");
                    return await tcs.Task;
                }
            }
            finally
            {
                //unsubscribe
                TheSerialPort.DataReceived -= handler;
            }

        }

        /// <summary>
        /// request TX battery voltage
        /// </summary>
        /// <returns>TX battery voltage</returns>
        public async Task<double> RequestTXBatteryVoltage()
        {
            TaskCompletionSource<double> tcs = new TaskCompletionSource<double>();

            //check presets
            if (TheSerialPort == null || !IsConnectionEstableshed)
            {
                tcs.SetException(new NullReferenceException("The serial port is not ready!"));
                return await tcs.Task;
            }

            //for cancellation of the Task after timeout
            CancellationTokenSource ct = new CancellationTokenSource();
            ct.CancelAfter(this.TimeOut);

            //handler for the device responce
            SerialDataReceivedEventHandler handler = (object sender, SerialDataReceivedEventArgs e) =>
            {
                Thread.Sleep(SleepTime);
                string str = string.Empty;
                if (TheSerialPort.BytesToRead > 0)
                    str = TheSerialPort.ReadExisting();
                double voltage;
                var val = Double.TryParse(str, out voltage);
                if (val)
                {
                    tcs.SetResult(voltage);
                    this.TXbattVoltage = voltage;
                }
                else
                {
                    if (str.StartsWith(this.BadResponce))
                    {
                        this.TXbattVoltage = defaultDoubleValue;
                        tcs.SetResult(TXbattVoltage);
                    }
                    else
                    {
                        if (!tcs.Task.IsCompleted)
                        {
                            tcs.SetException(new ArgumentException("Unacceptable TX voltage value!"));
                        }
                    }
                }
            };

            //subscribe to device responce
            TheSerialPort.DataReceived += handler;

            try
            {
                using (ct.Token.Register(() => tcs.SetCanceled(), false))//for cancellation
                {
                    TheSerialPort.WriteLine(":BATT?");
                    return await tcs.Task;
                }
            }
            finally
            {
                //unsubscribe
                TheSerialPort.DataReceived -= handler;
            }

        }

        /// <summary>
        /// request optic link level
        /// </summary>
        /// <returns>optic link level</returns>
        public async Task<double> RequestOptLinkLvl()
        {
            TaskCompletionSource<double> tcs = new TaskCompletionSource<double>();

            //check presets
            if (TheSerialPort == null || !IsConnectionEstableshed)
            {
                tcs.SetException(new NullReferenceException("The serial port is not ready!"));
                return await tcs.Task;
            }

            //for cancellation of the Task after timeout
            CancellationTokenSource ct = new CancellationTokenSource();
            ct.CancelAfter(this.TimeOut);

            //handler for the device responce
            SerialDataReceivedEventHandler handler = (object sender, SerialDataReceivedEventArgs e) =>
            {
                string str = string.Empty;
                if (TheSerialPort.BytesToRead > 0)
                    str = TheSerialPort.ReadLine();
                double signalLvl;
                var val = Double.TryParse(str, out signalLvl);
                if (val)
                {
                    tcs.SetResult(signalLvl);
                    this.OptLinkLvl = signalLvl;
                }
                else
                {
                    if (str.StartsWith(this.BadResponce))
                        this.OptLinkLvl = defaultDoubleValue;
                    else
                    {
                        if (!tcs.Task.IsCompleted)
                        {
                            tcs.SetException(new ArgumentException("Unacceptable value of optical link level!"));
                        }
                    }
                }
            };

            //subscribe to device responce
            TheSerialPort.DataReceived += handler;

            try
            {
                using (ct.Token.Register(() => tcs.SetCanceled(), false))//for cancellation
                {
                    TheSerialPort.WriteLine(":OPT?");
                    return await tcs.Task;
                }
            }
            finally
            {
                //unsubscribe
                TheSerialPort.DataReceived -= handler;
            }

        }

        /// <summary>
        /// send reset command
        /// </summary>
        /// <returns></returns>
        public async Task SendResetCmd()
        {
            TaskCompletionSource tcs = new TaskCompletionSource();

            //check presets
            if (TheSerialPort == null || !IsConnectionEstableshed)
            {
                tcs.SetException(new NullReferenceException("The serial port is not ready!"));
                await tcs.Task;
            }

            //for cancellation of the Task after timeout
            CancellationTokenSource ct = new CancellationTokenSource();
            ct.CancelAfter(this.TimeOut);

            //handler for the device responce
            SerialDataReceivedEventHandler handler = (object sender, SerialDataReceivedEventArgs e) =>
            {
                Thread.Sleep(5*SleepTime);
                string str = string.Empty;
                if (TheSerialPort.BytesToRead > 0)
                    str = TheSerialPort.ReadExisting();

                tcs.SetResult();
                return;
            };

            //subscribe to device responce
            TheSerialPort.DataReceived += handler;

            try
            {
                using (ct.Token.Register(() => tcs.SetCanceled(), false))//for cancellation
                {
                    TheSerialPort.WriteLine(":RST");
                    await tcs.Task;
                }
            }
            finally
            {
                //unsubscribe
                TheSerialPort.DataReceived -= handler;
            }
        }

        /// <summary>
        /// set test signal generator to ON or OFF
        /// </summary>
        /// <param name="on">is set to ON</param>
        /// <returns></returns>
        public async Task SetTestGenerator(bool on)
        {
            TaskCompletionSource tcs = new TaskCompletionSource();

            //check presets
            if (TheSerialPort == null || !IsConnectionEstableshed)
            {
                tcs.SetException(new NullReferenceException("The serial port is not ready!"));
                await tcs.Task;
            }

            //for cancellation of the Task after timeout
            CancellationTokenSource ct = new CancellationTokenSource();
            ct.CancelAfter(this.TimeOut);

            //handler for the device responce
            SerialDataReceivedEventHandler handler = (object sender, SerialDataReceivedEventArgs e) =>
            {
                Thread.Sleep(SleepTime);
                string str = string.Empty;
                if (TheSerialPort.BytesToRead > 0)
                    str = TheSerialPort.ReadExisting();

                if (str.StartsWith(BadResponce))
                {
                    tcs.SetException(new IOException("Error while switching test generator."));
                    return;
                }

                tcs.SetResult();
                return;
            };

            //subscribe to device responce
            TheSerialPort.DataReceived += handler;

            try
            {
                using (ct.Token.Register(() => tcs.SetCanceled(), false))//for cancellation
                {
                    TheSerialPort.WriteLine(":GEN"+ (on ? "1" : "0"));
                    await tcs.Task;
                }
            }
            finally
            {
                //unsubscribe
                TheSerialPort.DataReceived -= handler;
            }
        }

        /// <summary>
        /// set rx to low/normal power mode 
        /// </summary>
        /// <param name="isRX">true - for RX, false - TX</param>
        /// <param name="isLowPowerMode">true - low power mode, false - normal power mode</param>
        /// <returns></returns>
        public async Task SetUnitToLowPower(bool isRX,bool isLowPowerMode)
        {
            TaskCompletionSource tcs = new TaskCompletionSource();

            //check presets
            if (TheSerialPort == null || !IsConnectionEstableshed)
            {
                tcs.SetException(new NullReferenceException("The serial port is not ready!"));
                await tcs.Task;
            }

            //for cancellation of the Task after timeout
            CancellationTokenSource ct = new CancellationTokenSource();
            ct.CancelAfter(this.TimeOut);

            //handler for the device responce
            SerialDataReceivedEventHandler handler = (object sender, SerialDataReceivedEventArgs e) =>
            {
                Thread.Sleep(SleepTime);
                string str = string.Empty;
                if (TheSerialPort.BytesToRead > 0)
                    str = TheSerialPort.ReadExisting();

                if (str.StartsWith(BadResponce))
                {
                    tcs.SetException(new IOException("Error while switching power mode."));
                    return;
                }

                tcs.SetResult();
                return;
            };

            //subscribe to device responce
            TheSerialPort.DataReceived += handler;

            try
            {
                using (ct.Token.Register(() => tcs.SetCanceled(), false))//for cancellation
                {
                    TheSerialPort.WriteLine(":"+(isRX?"RX":"TX") + (!isLowPowerMode ? "1" : "0"));
                    await tcs.Task;
                }
            }
            finally
            {
                //unsubscribe
                TheSerialPort.DataReceived -= handler;
            }
        }

        /// <summary>
        /// set attenuation value
        /// </summary>
        /// <param name="attValue">attenuation value (0dB ... 31.75dB)</param>
        /// <returns></returns>
        public async Task SetAttenuation(double attValue)
        {
            TaskCompletionSource tcs = new TaskCompletionSource();

            //check presets
            if (TheSerialPort == null || !IsConnectionEstableshed)
            {
                tcs.SetException(new NullReferenceException("The serial port is not ready!"));
                await tcs.Task;
            }

            //internal representation of attenuation value
            int attInternalValue = (int)Math.Round(attValue * 10);
            if (attInternalValue < 0)
                attInternalValue = 0;//min val
            else
                if (attInternalValue > 318)
                    attInternalValue = 318;//max val

            //for cancellation of the Task after timeout
            CancellationTokenSource ct = new CancellationTokenSource();
            ct.CancelAfter(this.TimeOut);

            //handler for the device responce
            SerialDataReceivedEventHandler handler = (object sender, SerialDataReceivedEventArgs e) =>
            {
                Thread.Sleep(SleepTime);
                string str = string.Empty;
                if (TheSerialPort.BytesToRead > 0)
                    str = TheSerialPort.ReadExisting();

                if (str.StartsWith(BadResponce))
                {
                    tcs.SetException(new IOException("Error while setting attenuation value."));
                    return;
                }

                tcs.SetResult();
                return;
            };

            //subscribe to device responce
            TheSerialPort.DataReceived += handler;

            try
            {
                using (ct.Token.Register(() => tcs.SetCanceled(), false))//for cancellation
                {
                    string strCommand  = String.Format(":ATT{0:000}",attInternalValue);
                    TheSerialPort.WriteLine(strCommand);
                    //TheSerialPort.WriteLine($":ATT?");
                    await tcs.Task;
                }
            }
            finally
            {
                //unsubscribe
                TheSerialPort.DataReceived -= handler;
            }
        }
        #endregion
    }
}
