using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Serialization;
using Timer = System.Windows.Forms.Timer;

namespace Laba3
{
    public partial class MainForm : Form
    {
        private readonly BindingList<CarBrand> _brands = new BindingList<CarBrand>();
        private readonly BindingSource _brandBindingSource = new BindingSource();
        private readonly XmlSerializer _serializer = new XmlSerializer(typeof(List<CarBrand>));

        private DataGridView _brandsGrid;
        private DataGridView _carsGrid;
        private ProgressBar _progressBar;
        private Timer _progressTimer;
        private CancellationTokenSource _loadCts;
        private Button _arenaButton;
        private CarType _currentBrandType;
        private bool _isArenaBattleActive;

        // Loader management
        private readonly Dictionary<string, LoaderClient> _loaders = new Dictionary<string, LoaderClient>();
        private readonly Dictionary<string, Panel> _loaderStatusPanels = new Dictionary<string, Panel>();
        private readonly Dictionary<int, LoaderServer> _localServers = new Dictionary<int, LoaderServer>();
        private Panel _loadersPanel;
        private Button _addLoaderButton;
        private TextBox _loaderIpTextBox;
        private TextBox _loaderPortTextBox;

        private readonly Color _passengerColor = Color.FromArgb(210, 236, 255);
        private readonly Color _truckColor = Color.FromArgb(255, 232, 208);
        private readonly Color _gridAccent = Color.FromArgb(42, 48, 66);

        public MainForm()
        {
            InitializeComponent();
            InitializeDataBinding();
            SeedInitialBrands();
            StartLocalServer();
        }

        private void StartLocalServer()
        {
            try
            {
                // Start multiple servers on different ports (8080-8084)
                for (int port = 8080; port <= 8084; port++)
                {
                    try
                    {
                        var server = new LoaderServer("0.0.0.0", port);
                        server.Start();
                        _localServers[port] = server;
                    }
                    catch (Exception ex)
                    {
                        // Port might be in use, skip it
                        System.Diagnostics.Debug.WriteLine($"Failed to start server on port {port}: {ex.Message}");
                    }
                }
                
                // Get all available IP addresses on this machine
                var availableIPs = new System.Text.StringBuilder();
                availableIPs.AppendLine($"Локальные серверы запущены на портах: {string.Join(", ", _localServers.Keys)}");
                availableIPs.AppendLine("\nВы можете подключиться используя любой из этих адресов:");
                
                foreach (var port in _localServers.Keys)
                {
                    availableIPs.AppendLine($"• 127.0.0.1:{port} (localhost)");
                }
                
                // Get network IPs
                var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        foreach (var port in _localServers.Keys)
                        {
                            availableIPs.AppendLine($"• {ip}:{port} (сетевой адрес)");
                        }
                        break; // Only show first network IP to avoid cluttering
                    }
                }
                
                MessageBox.Show(this, availableIPs.ToString(), "Серверы запущены", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Не удалось запустить локальные серверы: {ex.Message}", "Предупреждение", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void InitializeComponent()
        {
            SuspendLayout();

            Text = "Лабораторная №3 — Марки автомобилей";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1100, 680);
            BackColor = Color.FromArgb(245, 247, 252);

            // Menu
            var menuStrip = new MenuStrip
            {
                BackColor = Color.FromArgb(32, 36, 48),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold)
            };

            var fileMenu = new ToolStripMenuItem("Файл")
            {
                ForeColor = Color.White
            };

            var saveMenu = new ToolStripMenuItem("Сохранить список марок", null, SaveMenu_Click);
            var loadMenu = new ToolStripMenuItem("Загрузить", null, LoadMenu_Click);
            var exitMenu = new ToolStripMenuItem("Выход", null, (s, e) => Close());

            fileMenu.DropDownItems.AddRange(new ToolStripItem[] { saveMenu, loadMenu, exitMenu });
            menuStrip.Items.Add(fileMenu);
            MainMenuStrip = menuStrip;
            Controls.Add(menuStrip);

            // Loaders management panel
            var loadersPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 200,
                BackColor = Color.FromArgb(240, 242, 247),
                Padding = new Padding(10)
            };

            var ipLabel = new Label
            {
                Text = "IP:",
                Location = new Point(10, 20),
                AutoSize = true
            };

            _loaderIpTextBox = new TextBox
            {
                Location = new Point(40, 17),
                Width = 120,
                Text = "127.0.0.1"
            };

            var portLabel = new Label
            {
                Text = "Port:",
                Location = new Point(170, 20),
                AutoSize = true
            };

            _loaderPortTextBox = new TextBox
            {
                Location = new Point(210, 17),
                Width = 80,
                Text = "8080"
            };

            _addLoaderButton = new Button
            {
                Text = "Добавить лоадер",
                Location = new Point(300, 15),
                Width = 140,
                Height = 30,
                BackColor = Color.FromArgb(76, 175, 80),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _addLoaderButton.FlatAppearance.BorderSize = 0;
            _addLoaderButton.Click += AddLoaderButton_Click;

            _loadersPanel = new Panel
            {
                Location = new Point(10, 50),
                AutoScroll = true,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };

            loadersPanel.Controls.AddRange(new Control[] { ipLabel, _loaderIpTextBox, portLabel, _loaderPortTextBox, _addLoaderButton, _loadersPanel });
            
            // Set size after adding to parent so ClientSize is correct
            _loadersPanel.Size = new Size(loadersPanel.ClientSize.Width - 20, loadersPanel.ClientSize.Height - 60);

            Controls.Add(loadersPanel);

            // Progress bar
            _progressBar = new ProgressBar
            {
                Dock = DockStyle.Bottom,
                Height = 24,
                Style = ProgressBarStyle.Continuous,
                Visible = true
            };
            Controls.Add(_progressBar);

            // Split container
            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                SplitterDistance = 560,
                BackColor = Color.Transparent
            };
            Controls.Add(split);
            Controls.SetChildIndex(split, 0);

            // Brands grid
            _brandsGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = true,
                AllowUserToDeleteRows = true,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                BackgroundColor = Color.White,
                GridColor = _gridAccent,
                AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(248, 251, 255)
                }
            };

            _brandsGrid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = _gridAccent,
                ForeColor = Color.White,
                Alignment = DataGridViewContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold)
            };
            _brandsGrid.EnableHeadersVisualStyles = false;

            _brandsGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "BrandName",
                HeaderText = "Марка",
                Width = 120
            });
            _brandsGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "ModelName",
                HeaderText = "Модель",
                Width = 150
            });
            _brandsGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "HorsePower",
                HeaderText = "Мощность (л.с.)",
                Width = 130
            });
            _brandsGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "MaxSpeed",
                HeaderText = "Макс. скорость",
                Width = 130
            });
            var typeColumn = new DataGridViewComboBoxColumn
            {
                DataPropertyName = "Type",
                HeaderText = "Тип",
                Width = 120,
                DataSource = new[]
                {
                    new KeyValuePair<CarType, string>(CarType.Passenger, "Легковой"),
                    new KeyValuePair<CarType, string>(CarType.Truck, "Грузовой")
                },
                DisplayMember = "Value",
                ValueMember = "Key",
                ValueType = typeof(CarType)
            };
            _brandsGrid.Columns.Add(typeColumn);

            _brandsGrid.SelectionChanged += BrandsGrid_SelectionChanged;
            _brandsGrid.CellFormatting += BrandsGrid_CellFormatting;
            _brandsGrid.CellValueChanged += BrandsGrid_CellValueChanged;
            _brandsGrid.CurrentCellDirtyStateChanged += BrandsGrid_CurrentCellDirtyStateChanged;
            _brandsGrid.DataError += BrandsGrid_DataError;

            split.Panel1.Controls.Add(new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
                BackColor = Color.Transparent,
                Controls = { _brandsGrid }
            });

            // Cars grid
            _carsGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                BackgroundColor = Color.White,
                GridColor = _gridAccent,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };

            _carsGrid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(54, 60, 78),
                ForeColor = Color.White,
                Alignment = DataGridViewContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold)
            };
            _carsGrid.EnableHeadersVisualStyles = false;

            // Arena button
            _arenaButton = new Button
            {
                Text = "Арена",
                Dock = DockStyle.Top,
                Height = 40,
                BackColor = Color.FromArgb(220, 53, 69),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            _arenaButton.FlatAppearance.BorderSize = 0;
            _arenaButton.Click += ArenaButton_Click;

            split.Panel2.Controls.Add(new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
                BackColor = Color.Transparent,
                Controls = { _carsGrid, _arenaButton }
            });

            // Progress timer
            _progressTimer = new Timer { Interval = 120 };
            _progressTimer.Tick += ProgressTimer_Tick;

            FormClosing += MainForm_FormClosing;

            ResumeLayout(false);
            PerformLayout();
        }

        private void InitializeDataBinding()
        {
            _brandBindingSource.DataSource = _brands;
            _brandBindingSource.AddingNew += (s, e) => e.NewObject = new CarBrand();
            _brandsGrid.DataSource = _brandBindingSource;
        }

        private void SeedInitialBrands()
        {
            _brands.Add(new CarBrand { BrandName = "Audi", ModelName = "A6", HorsePower = 245, MaxSpeed = 240, Type = CarType.Passenger });
            _brands.Add(new CarBrand { BrandName = "Kamaz", ModelName = "6520", HorsePower = 400, MaxSpeed = 150, Type = CarType.Truck });
            _brands.Add(new CarBrand { BrandName = "Tesla", ModelName = "Model S", HorsePower = 670, MaxSpeed = 265, Type = CarType.Passenger });
            _brands.Add(new CarBrand { BrandName = "Volvo", ModelName = "FMX", HorsePower = 540, MaxSpeed = 180, Type = CarType.Truck });
        }

        private void BrandsGrid_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (_brandsGrid.IsCurrentCellDirty && _brandsGrid.CurrentCell is DataGridViewComboBoxCell)
            {
                _brandsGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        }

        private void BrandsGrid_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            MessageBox.Show(this, "Некорректное значение. Проверьте ввод.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            e.ThrowException = false;
        }

        private void BrandsGrid_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _brandsGrid.Rows.Count)
            {
                return;
            }

            var row = _brandsGrid.Rows[e.RowIndex];
            var brand = row.DataBoundItem as CarBrand;
            if (brand == null)
            {
                return;
            }

            UpdateRowColor(row, brand.Type);
        }

        private async void BrandsGrid_SelectionChanged(object sender, EventArgs e)
        {
            if (_brandsGrid.SelectedRows.Count == 0)
            {
                _carsGrid.Rows.Clear();
                _carsGrid.Columns.Clear();
                return;
            }

            var row = _brandsGrid.SelectedRows[0];
            var brand = row.DataBoundItem as CarBrand;
            if (brand == null)
            {
                return;
            }

            await LoadCarsForBrandAsync(brand);
        }

        private async Task LoadCarsForBrandAsync(CarBrand brand)
        {
            _loadCts?.Cancel();
            _loadCts = new CancellationTokenSource();

            ConfigureCarsGrid(brand.Type);
            _progressBar.Value = 0;
            _progressTimer.Start();

            try
            {
                var cars = await Loader.LoadAsync(brand, _loadCts.Token).ConfigureAwait(true);
                PopulateCarsGrid(brand.Type, cars);
            }
            catch (OperationCanceledException)
            {
                // ignore
            }
            finally
            {
                _progressTimer.Stop();
                _progressBar.Value = 0;
            }
        }

        private void ConfigureCarsGrid(CarType type)
        {
            _carsGrid.Columns.Clear();
            _currentBrandType = type;

            _carsGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Регистрационный номер",
                Width = 200
            });

            if (type == CarType.Passenger)
            {
                _carsGrid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    HeaderText = "Мультимедиа",
                    Width = 220
                });
                _carsGrid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    HeaderText = "Подушек безопасности",
                    Width = 180
                });
            }
            else
            {
                _carsGrid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    HeaderText = "Кол-во колёс",
                    Width = 120
                });
                _carsGrid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    HeaderText = "Объём кузова (м³)",
                    Width = 180
                });
            }
        }

        private void PopulateCarsGrid(CarType type, IEnumerable<ICar> cars)
        {
            _carsGrid.Rows.Clear();

            foreach (var car in cars)
            {
                DataGridViewRow row;
                if (type == CarType.Passenger && car is PassengerCar passenger)
                {
                    row = _carsGrid.Rows[_carsGrid.Rows.Add(passenger.RegistrationNumber, passenger.MultimediaName, passenger.AirbagCount)];
                    row.Tag = passenger;
                }
                else if (type == CarType.Truck && car is Truck truck)
                {
                    row = _carsGrid.Rows[_carsGrid.Rows.Add(truck.RegistrationNumber, truck.WheelCount, truck.BodyVolume)];
                    row.Tag = truck;
                }
                else
                {
                    continue;
                }
            }
        }

        private void BrandsGrid_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _brands.Count)
            {
                return;
            }

            var row = _brandsGrid.Rows[e.RowIndex];
            var brand = row.DataBoundItem as CarBrand;
            if (brand == null)
            {
                return;
            }

            UpdateRowColor(row, brand.Type);

            if (_brandsGrid.Columns[e.ColumnIndex].DataPropertyName == "Type")
            {
                Loader.Invalidate(brand.Id);
                if (row.Selected)
                {
                    _ = LoadCarsForBrandAsync(brand);
                }
            }
        }

        private void UpdateRowColor(DataGridViewRow row, CarType type)
        {
            row.DefaultCellStyle.BackColor = type == CarType.Passenger ? _passengerColor : _truckColor;
            row.DefaultCellStyle.SelectionBackColor = type == CarType.Passenger
                ? Color.FromArgb(140, 190, 230)
                : Color.FromArgb(255, 190, 140);
        }

        private void ProgressTimer_Tick(object sender, EventArgs e)
        {
            var progress = Loader.GetProgress();
            int value = (int)Math.Round(progress * _progressBar.Maximum);
            value = Math.Max(_progressBar.Minimum, Math.Min(_progressBar.Maximum, value));
            _progressBar.Value = value;
        }

        private void SaveMenu_Click(object sender, EventArgs e)
        {
            using (var dialog = new SaveFileDialog
            {
                Filter = "XML файлы (*.xml)|*.xml",
                Title = "Сохранить список марок"
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                try
                {
                    using (var stream = File.Create(dialog.FileName))
                    {
                        _serializer.Serialize(stream, _brands.ToList());
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Не удалось сохранить файл: " + ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void LoadMenu_Click(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog
            {
                Filter = "XML файлы (*.xml)|*.xml",
                Title = "Загрузить список марок"
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                try
                {
                    using (var stream = File.OpenRead(dialog.FileName))
                    {
                        var loaded = _serializer.Deserialize(stream) as List<CarBrand>;
                        if (loaded != null)
                        {
                            _brands.Clear();
                            foreach (var brand in loaded)
                            {
                                if (string.IsNullOrWhiteSpace(brand.Id))
                                {
                                    brand.Id = Guid.NewGuid().ToString();
                                }
                                _brands.Add(brand);
                            }
                            Loader.Clear();
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Не удалось загрузить файл: " + ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_loadCts != null)
            {
                _loadCts.Cancel();
            }
        }

        private void ArenaButton_Click(object sender, EventArgs e)
        {
            if (_isArenaBattleActive)
            {
                MessageBox.Show(this, "Битва уже идет!", "Арена", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_carsGrid.Rows.Count == 0)
            {
                MessageBox.Show(this, "Нет автомобилей для битвы!", "Арена", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Добавляем строку "Арена" в середину таблицы
            int middleIndex = _carsGrid.Rows.Count / 2;
            var arenaRow = new DataGridViewRow();
            arenaRow.CreateCells(_carsGrid, "Арена", "Арена", "Арена");
            _carsGrid.Rows.Insert(middleIndex, arenaRow);
            arenaRow.Tag = "ARENA";
            arenaRow.DefaultCellStyle.BackColor = Color.FromArgb(139, 0, 0);
            arenaRow.DefaultCellStyle.ForeColor = Color.White;
            arenaRow.DefaultCellStyle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);

            _arenaButton.Enabled = false;
            _isArenaBattleActive = true;

            _ = StartArenaBattleAsync();
        }

        private async Task StartArenaBattleAsync()
        {
            // Находим индекс строки "Арена"
            int arenaIndex = -1;
            for (int i = 0; i < _carsGrid.Rows.Count; i++)
            {
                if (_carsGrid.Rows[i].Tag != null && _carsGrid.Rows[i].Tag.ToString() == "ARENA")
                {
                    arenaIndex = i;
                    break;
                }
            }

            if (arenaIndex == -1)
            {
                _isArenaBattleActive = false;
                _arenaButton.Enabled = true;
                return;
            }

            while (true)
            {
                await Task.Delay(1500); // Увеличена задержка между битвами

                // Находим верхнюю строку (выше арены)
                DataGridViewRow topCar = null;
                for (int i = arenaIndex - 1; i >= 0; i--)
                {
                    if (_carsGrid.Rows[i].Tag != null && _carsGrid.Rows[i].Tag.ToString() != "ARENA")
                    {
                        topCar = _carsGrid.Rows[i];
                        break;
                    }
                }

                // Находим нижнюю строку (ниже арены)
                DataGridViewRow bottomCar = null;
                for (int i = arenaIndex + 1; i < _carsGrid.Rows.Count; i++)
                {
                    if (_carsGrid.Rows[i].Tag != null && _carsGrid.Rows[i].Tag.ToString() != "ARENA")
                    {
                        bottomCar = _carsGrid.Rows[i];
                        break;
                    }
                }

                // Если нет строк с обеих сторон - битва окончена
                if (topCar == null || bottomCar == null)
                {
                    break;
                }

                var car1 = topCar.Tag as ICar;
                var car2 = bottomCar.Tag as ICar;
                
                if (car1 == null || car2 == null)
                {
                    continue;
                }

                var strength1 = car1.GetStrength();
                var strength2 = car2.GetStrength();

                // Подсвечиваем сражающихся
                topCar.DefaultCellStyle.BackColor = Color.Yellow;
                bottomCar.DefaultCellStyle.BackColor = Color.Yellow;
                _carsGrid.Refresh();
                await Task.Delay(1000); // Увеличена задержка подсветки

                // Удаляем проигравшего
                DataGridViewRow loser = strength1 < strength2 ? topCar : bottomCar;
                DataGridViewRow winner = strength1 >= strength2 ? topCar : bottomCar;

                // Возвращаем цвет победителю
                if (winner.Tag is PassengerCar)
                {
                    winner.DefaultCellStyle.BackColor = _passengerColor;
                }
                else if (winner.Tag is Truck)
                {
                    winner.DefaultCellStyle.BackColor = _truckColor;
                }

                int loserIndex = loser.Index;
                _carsGrid.Rows.Remove(loser);

                // Обновляем индекс арены, если удалили строку выше неё
                if (loserIndex < arenaIndex)
                {
                    arenaIndex--;
                }

                _carsGrid.Refresh();
            }

            _isArenaBattleActive = false;
            _arenaButton.Enabled = true;

            var remainingCars = 0;
            foreach (DataGridViewRow row in _carsGrid.Rows)
            {
                if (row.Tag != null && row.Tag.ToString() != "ARENA")
                {
                    remainingCars++;
                }
            }

            if (remainingCars == 1)
            {
                MessageBox.Show(this, "Битва завершена! Остался один победитель!", "Арена", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(this, "Битва завершена!", "Арена", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private async void AddLoaderButton_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_loaderIpTextBox.Text) || string.IsNullOrWhiteSpace(_loaderPortTextBox.Text))
            {
                MessageBox.Show(this, "Введите IP и Port!", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!int.TryParse(_loaderPortTextBox.Text, out int port))
            {
                MessageBox.Show(this, "Некорректный порт!", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string key = $"{_loaderIpTextBox.Text}:{port}";

            if (_loaders.ContainsKey(key))
            {
                MessageBox.Show(this, "Лоадер уже добавлен!", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var client = new LoaderClient(_loaderIpTextBox.Text, port);
            client.BrandReceived += (brand) =>
            {
                if (InvokeRequired)
                {
                    BeginInvoke(new Action(() => AddBrandFromLoader(brand)));
                }
                else
                {
                    AddBrandFromLoader(brand);
                }
            };

            client.ConnectionStatusChanged += (isConnected) =>
            {
                if (InvokeRequired)
                {
                    BeginInvoke(new Action(() => UpdateLoaderStatus(key, isConnected)));
                }
                else
                {
                    UpdateLoaderStatus(key, isConnected);
                }
            };

            _loaders[key] = client;
            CreateLoaderStatusPanel(key, _loaderIpTextBox.Text, port);
            
            // Disable the add button and show connecting status
            _addLoaderButton.Enabled = false;
            _addLoaderButton.Text = "Подключение...";
            
            try
            {
                // Try to connect with timeout and error handling
                var errorMessage = await client.ConnectAsync();
                
                if (!string.IsNullOrEmpty(errorMessage))
                {
                    // Connection failed - show error and remove the loader
                    MessageBox.Show(this, errorMessage, "Ошибка подключения", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    
                    // Clean up failed loader
                    _loaders.Remove(key);
                    if (_loaderStatusPanels.ContainsKey(key))
                    {
                        var panel = _loaderStatusPanels[key];
                        _loaderStatusPanels.Remove(key);
                        _loadersPanel.Controls.Remove(panel);
                        panel?.Dispose();
                    }
                }
                else
                {
                    // Connection successful
                    MessageBox.Show(this, $"Успешно подключено к {_loaderIpTextBox.Text}:{port}", "Успех", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            finally
            {
                // Re-enable the add button
                _addLoaderButton.Enabled = true;
                _addLoaderButton.Text = "Добавить лоадер";
            }
        }

        private void AddBrandFromLoader(CarBrand brand)
        {
            if (brand != null)
            {
                _brands.Add(brand);
            }
        }

        private void CreateLoaderStatusPanel(string key, string ip, int port)
        {
            var panel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 40,
                BackColor = Color.White,
                Margin = new Padding(5),
                Padding = new Padding(5)
            };

            var statusCircle = new Panel
            {
                Width = 20,
                Height = 20,
                Location = new Point(10, 10),
                BackColor = Color.Red,
                BorderStyle = BorderStyle.FixedSingle
            };
            statusCircle.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.FillEllipse(new SolidBrush(statusCircle.BackColor), 0, 0, statusCircle.Width - 1, statusCircle.Height - 1);
            };

            var label = new Label
            {
                Text = $"{ip}:{port}",
                Location = new Point(40, 10),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F)
            };

            var requestButton = new Button
            {
                Text = "Запросить",
                Dock = DockStyle.Right,
                Width = 90,
                BackColor = Color.FromArgb(33, 150, 243),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            requestButton.FlatAppearance.BorderSize = 0;
            requestButton.Click += (s, e) =>
            {
                if (!_loaders.ContainsKey(key))
                {
                    return;
                }

                if (_loaders[key].IsConnected)
                {
                    // Send a single request
                    byte[] request = new byte[] { 1 };
                    _loaders[key].SendRequest(request);
                }
                else
                {
                    MessageBox.Show(this, "Лоадер не подключен!", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };

            var removeButton = new Button
            {
                Text = "Удалить",
                Dock = DockStyle.Right,
                Width = 80,
                BackColor = Color.FromArgb(244, 67, 54),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            removeButton.FlatAppearance.BorderSize = 0;
            removeButton.Click += (s, e) =>
            {
                try
                {
                    // Disconnect and remove the loader
                    if (_loaders.ContainsKey(key))
                    {
                        _loaders[key]?.Disconnect();
                        _loaders.Remove(key);
                    }
                    
                    // Remove from status panels dictionary
                    _loaderStatusPanels.Remove(key);
                    
                    // Remove the panel from the UI
                    if (_loadersPanel != null && panel != null && _loadersPanel.Controls.Contains(panel))
                    {
                        _loadersPanel.Controls.Remove(panel);
                    }
                    
                    // Dispose the panel to free resources
                    panel?.Dispose();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Ошибка при удалении лоадера: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            panel.Controls.AddRange(new Control[] { statusCircle, label, removeButton, requestButton });
            panel.Tag = statusCircle;

            _loaderStatusPanels[key] = panel;
            _loadersPanel.Controls.Add(panel);
            _loadersPanel.Controls.SetChildIndex(panel, 0);
        }

        private void UpdateLoaderStatus(string key, bool isConnected)
        {
            if (_loaderStatusPanels.ContainsKey(key))
            {
                var panel = _loaderStatusPanels[key];
                var statusCircle = panel.Tag as Panel;
                if (statusCircle != null)
                {
                    statusCircle.BackColor = isConnected ? Color.Green : Color.Red;
                    statusCircle.Invalidate();
                }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Disconnect all loaders
            foreach (var loader in _loaders.Values)
            {
                loader.Disconnect();
            }
            _loaders.Clear();

            // Stop all local servers
            foreach (var server in _localServers.Values)
            {
                server?.Stop();
            }
            _localServers.Clear();

            if (_loadCts != null)
            {
                _loadCts.Cancel();
            }
            base.OnFormClosing(e);
        }
    }
}

