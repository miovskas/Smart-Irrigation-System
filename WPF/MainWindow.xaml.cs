using System.Text;
using System.IO.Ports;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace IrrigationSystem
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private SerialPort? serialPort;
        private int testStep = 0;
        private string previousPumpState = "OFF";
        public MainWindow()
        {
            InitializeComponent();
            LoadComPorts();
            


        }
        private void AddHistoryEntry(string data)
        {
            string time = DateTime.Now.ToString("HH:mm:ss");

            string soil = "---";
            string water = "---";
            string temp = "---";
            string humidity = "---";
            string pump = "---";
            string mode = "---";

            string[] parts = data.Split(';');

            foreach (string part in parts)
            {
                string[] pair = part.Split(':');

                if (pair.Length != 2)
                    continue;

                string key = pair[0];
                string value = pair[1].Trim();

                if (key == "SOIL")
                    soil = value;

                if (key == "WATER")
                    water = value;

                if (key == "TEMP")
                    temp = value;

                if (key == "HUM")
                    humidity = value;

                if (key == "PUMP")
                    pump = value;

                if (key == "MODE")
                    mode = value;
            }


            string soilDisplay = "---";
            string waterDisplay = "---";

            if (int.TryParse(soil.Trim(), out int soilRaw))
            {
                double soilPercent =
                    (1023.0 - soilRaw) / (1023.0 - 115.0) * 100.0;

                soilPercent = Math.Clamp(soilPercent, 0, 100);
                soilDisplay = $"{soilPercent:F0}%";
            }

            if (int.TryParse(water.Trim(), out int waterRaw))
            {
                double waterPercent = waterRaw / 600.0 * 100.0;

                waterPercent = Math.Clamp(waterPercent, 0, 100);
                waterDisplay = $"{waterPercent:F0}%";
            }

            string entry =
                $"{time} | Почва: {soilDisplay} | Вода: {waterDisplay} | " +
                $"Темп.: {temp} °C | Влажност: {humidity} % | " +
                $"Помпа: {pump} | Режим: {mode}";

            HistoryListBox.Items.Insert(0, entry);
            if (HistoryListBox.Items.Count > 100)
            {
                HistoryListBox.Items.RemoveAt(
                    HistoryListBox.Items.Count - 1
                );
            }
        }
            private void ClearHistoryButton_Click(object sender, RoutedEventArgs e)
        {
            HistoryListBox.Items.Clear();
        }
        private void TestDataButton_Click(object sender, RoutedEventArgs e)
        {
            TestModeIndicator.Visibility = Visibility.Visible;
            testStep++;

            if (testStep == 1)
            {
                ProcessArduinoData(
                    "SOIL:550;WATER:850;TEMP:24.0;HUM:60;PUMP:OFF;MODE:AUTO;ERROR:NONE;STATUS:OK"
                );
            }

            if (testStep == 2)
            {
                ProcessArduinoData(
                    "SOIL:250;WATER:820;TEMP:24.3;HUM:58;PUMP:ON;MODE:AUTO;ERROR:NONE;STATUS:OK"
                );
            }

            if (testStep == 3)
            {
                ProcessArduinoData(
                    "SOIL:450;WATER:780;TEMP:24.5;HUM:57;PUMP:OFF;MODE:AUTO;ERROR:NONE;STATUS:WAITING"
                );
            }

            if (testStep == 4)
            {
                ProcessArduinoData(
                    "SOIL:250;WATER:150;TEMP:24.5;HUM:57;PUMP:OFF;MODE:AUTO;ERROR:LOW_WATER;STATUS:ERROR"
                );
            }

            if (testStep == 5)
            {
                ProcessArduinoData(
                    "SOIL:400;WATER:800;TEMP:75.0;HUM:55;PUMP:OFF;MODE:AUTO;ERROR:SENSOR;STATUS:ERROR"
                );

                testStep = 0;
            }
        }




        private void LoadComPorts()
        {
            string[] ports = SerialPort.GetPortNames();

            PortComboBox.Items.Clear();

            foreach (string port in ports)
            {
                PortComboBox.Items.Add(port);
            }

            if (PortComboBox.Items.Count > 0)
            {
                PortComboBox.SelectedIndex = 0;
            }
        }
        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            // Ако вече има връзка - прекъсваме я
            if (serialPort != null && serialPort.IsOpen)
            {
                serialPort.Close();

                ConnectionStatusText.Text = "Не е свързано";
                ConnectionStatusText.Foreground = Brushes.Red;

                ConnectButton.Content = "Свързване";
                PortComboBox.IsEnabled = true;

                return;
            }

            // Проверка дали е избран COM порт
            if (PortComboBox.SelectedItem == null)
            {
                MessageBox.Show("Моля, изберете COM порт.");
                return;
            }

            try
            {
                serialPort = new SerialPort(
                    PortComboBox.SelectedItem.ToString(),
                    9600
                );

                serialPort.NewLine = "\n";
                serialPort.DataReceived += SerialPort_DataReceived;

                serialPort.Open();

                ConnectionStatusText.Text = "Свързано";
                ConnectionStatusText.Foreground = Brushes.Green;

                ConnectButton.Content = "Прекъсване";
                PortComboBox.IsEnabled = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Грешка при свързване:\n" + ex.Message,
                    "Грешка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }
        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                if (serialPort == null || !serialPort.IsOpen)
                    return;

                string data = serialPort.ReadLine();

                Dispatcher.Invoke(() =>
                {
                    ProcessArduinoData(data);
                });
            }
            catch
            {
                // При грешка в четенето приложението продължава да работи
            }
        }
        private void ProcessArduinoData(string data)
        {
            AddHistoryEntry(data);
            string[] parts = data.Split(';');

            foreach (string part in parts)
            {
                string[] pair = part.Split(':');

                if (pair.Length != 2)
                    continue;

                string key = pair[0];
                string value = pair[1].Trim();

                if (key == "SOIL")
                {
                    if (int.TryParse(value, out int soilRaw))
                    {
                        double soilPercent =
                            (1023.0 - soilRaw) / (1023.0 - 115.0) * 100.0;

                        soilPercent = Math.Clamp(soilPercent, 0, 100);

                        SoilValueText.Text = $"{soilPercent:F0} %";

                        if (soilPercent < 25)
                        {
                            SoilValueText.Foreground = Brushes.Red;
                        }
                        else if (soilPercent <= 40)
                        {
                            SoilValueText.Foreground = Brushes.Orange;
                        }
                        else
                        {
                            SoilValueText.Foreground = Brushes.Green;
                        }

                    }
                    else
                    {
                        SoilValueText.Text = "--- %";
                    }
                }


                if (key == "WATER")
                {
                    if (int.TryParse(value, out int waterRaw))
                    {
                        double waterPercent = waterRaw / 600.0 * 100.0;

                        waterPercent = Math.Clamp(waterPercent, 0, 100);

                        WaterValueText.Text = $"{waterPercent:F0} %";

                        if (waterRaw <= 200)
                        {
                            WaterValueText.Foreground = Brushes.Red;
                        }
                        else if (waterPercent <= 60)
                        {
                            WaterValueText.Foreground = Brushes.Orange;
                        }
                        else
                        {
                            WaterValueText.Foreground = Brushes.Green;
                        }

                    }
                    else
                    {
                        WaterValueText.Text = "--- %";
                    }
                }


                if (key == "TEMP")
                {
                    TemperatureValueText.Text = value + " °C";
                }

                if (key == "HUM")
                {
                    HumidityValueText.Text = value + " %";
                }

                if (key == "PUMP")
                {
                    PumpValueText.Text = value;

                    if (value == "ON")
                    {
                        PumpValueText.Foreground = Brushes.Green;
                    }

                    if (value == "OFF")
                    {
                        PumpValueText.Foreground = Brushes.Gray;

                        if (previousPumpState == "ON")
                        {
                            LastIrrigationText.Text =
                                DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss");

                            LastIrrigationText.Foreground = Brushes.Green;
                        }
                    }

                    previousPumpState = value;
                }

                if (key == "MODE")
                {
                    ModeValueText.Text = value;
                }
                if (key == "ERROR")
                {
                    if (value == "NONE")
                    {
                        WarningText.Text = "";
                    }

                    if (value == "LOW_WATER")
                    {
                        WarningText.Text = "Предупреждение: Ниско ниво на водата";
                    }

                    if (value == "SENSOR")
                    {
                        WarningText.Text = "Грешка в сензор";
                    }
                }

                if (key == "STATUS")
                {
                    

                    if (value == "OK")
                    {
                        SystemStatusText.Text = "Системата работи нормално";
                        SystemStatusText.Foreground = Brushes.Green;
                    }
                    else if (value == "WAITING")
                    {
                        SystemStatusText.Text = "Изчакване преди следващо поливане";
                        SystemStatusText.Foreground = Brushes.Orange;
                    }
                    else if (value == "ERROR")
                    {
                        if (WarningText.Text.Contains("Ниско ниво на водата"))
                        {
                            SystemStatusText.Text = "Поливането е блокирано";
                        }
                        else if (WarningText.Text.Contains("Грешка в сензор"))
                        {
                            SystemStatusText.Text = "Грешка в сензор";
                        }
                        else
                        {
                            SystemStatusText.Text = "Грешка в системата";
                        }

                        SystemStatusText.Foreground = Brushes.Red;
                    }
                }
            }
        }
        private void AutoButton_Click(object sender, RoutedEventArgs e)
        {
            if (serialPort != null && serialPort.IsOpen)
            {
                serialPort.WriteLine("AUTO");
            }
            else
            {
                MessageBox.Show("Няма връзка с Arduino.");
            }
        }
        private void ManualOnButton_Click(object sender, RoutedEventArgs e)
        {
            if (serialPort != null && serialPort.IsOpen)
            {
                serialPort.WriteLine("MANUAL ON");
            }
            else
            {
                MessageBox.Show("Няма връзка с Arduino.");
            }
        }

        private void ManualOffButton_Click(object sender, RoutedEventArgs e)
        {
            if (serialPort != null && serialPort.IsOpen)
            {
                serialPort.WriteLine("MANUAL OFF");
            }
            else
            {
                MessageBox.Show("Няма връзка с Arduino.");
            }
        }
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (serialPort != null && serialPort.IsOpen)
            {
                serialPort.Close();
            }
        }
    }
}