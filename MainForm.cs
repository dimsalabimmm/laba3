using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Serialization;
using Timer = System.Windows.Forms.Timer;

namespace Laba3
{
    public partial class MainForm : Form
    {
        private BindingSource _bindingSource = null!;
        private BindingList<ICarBrand> _brands = null!;

        private DataGridView _brandsGrid = null!;
        private DataGridView _carsGrid = null!;
        private ProgressBar _progressBar = null!;
        private Timer _progressTimer = null!;
        private Thread? _loadingThread;

        private static readonly XmlSerializer BrandSerializer =
            new XmlSerializer(typeof(List<CarBrandBase>), new[] { typeof(PassengerCarBrand), typeof(TruckCarBrand) });

        public MainForm()
        {
            InitializeComponent();
            InitializeDataBinding();
        }

        private void InitializeComponent()
        {
            SuspendLayout();

            Text = "Лабораторная №3 — Управление марками авто";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1050, 650);

            // Menu strip
            var menuStrip = new MenuStrip();
            var fileMenu = new ToolStripMenuItem("Файл");
            var saveMenu = new ToolStripMenuItem("Сохранить список марок", null, SaveMenu_Click);
            var loadMenu = new ToolStripMenuItem("Загрузить", null, LoadMenu_Click);
            var exitMenu = new ToolStripMenuItem("Выход", null, (_, __) => Close());
            fileMenu.DropDownItems.AddRange(new[] { saveMenu, loadMenu, exitMenu });
            menuStrip.Items.Add(fileMenu);
            MainMenuStrip = menuStrip;
            Controls.Add(menuStrip);

            // Progress bar docked bottom
            _progressBar = new ProgressBar
            {
                Dock = DockStyle.Bottom,
                Height = 24,
                Minimum = 0,
                Maximum = 100
            };
            Controls.Add(_progressBar);

            // Split container (left brands, right cars)
            var splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterWidth = 5
            };
            Controls.Add(splitContainer);
            Controls.SetChildIndex(splitContainer, 0); // ensure menu strip stays top

            // Brands grid
            _brandsGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = true,
                AllowUserToDeleteRows = true,
                AutoGenerateColumns = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };

            _brandsGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "BrandName",
                HeaderText = "Марка",
                DataPropertyName = "BrandName"
            });
            _brandsGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ModelName",
                HeaderText = "Модель",
                DataPropertyName = "ModelName"
            });
            _brandsGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Horsepower",
                HeaderText = "Мощность (л.с.)",
                DataPropertyName = "Horsepower"
            });
            _brandsGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "MaxSpeed",
                HeaderText = "Макс. скорость",
                DataPropertyName = "MaxSpeed"
            });
            _brandsGrid.Columns.Add(new DataGridViewComboBoxColumn
            {
                Name = "Type",
                HeaderText = "Тип",
                DataPropertyName = "Type",
                DataSource = Enum.GetValues(typeof(CarType))
            });

            _brandsGrid.SelectionChanged += BrandsGrid_SelectionChanged;
            _brandsGrid.CellFormatting += BrandsGrid_CellFormatting;
            _brandsGrid.CellValueChanged += BrandsGrid_CellValueChanged;
            _brandsGrid.DataError += (s, e) => e.ThrowException = false;

            splitContainer.Panel1.Controls.Add(_brandsGrid);

            // Cars grid (manual fill)
            _carsGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false
            };

            splitContainer.Panel2.Controls.Add(_carsGrid);

            // Progress timer for loader feedback
            _progressTimer = new Timer { Interval = 100 };
            _progressTimer.Tick += (_, __) => _progressBar.Value = Math.Min(Loader.GetProgress(), 100);

            FormClosing += MainForm_FormClosing;

            ResumeLayout(false);
            PerformLayout();
        }

        private void InitializeDataBinding()
        {
            _brands = new BindingList<ICarBrand>();
            _bindingSource = new BindingSource { DataSource = _brands };
            _bindingSource.AddingNew += (_, e) => e.NewObject = new PassengerCarBrand();
            _brandsGrid.DataSource = _bindingSource;
        }

        private void BrandsGrid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;

            var row = _brandsGrid.Rows[e.RowIndex];
            if (row.DataBoundItem is not ICarBrand brand) return;

            row.DefaultCellStyle.BackColor = brand.Type == CarType.Passenger
                ? Color.LightSkyBlue
                : Color.LightSalmon;
        }

        private void BrandsGrid_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _brands.Count) return;
            if (_brandsGrid.Columns[e.ColumnIndex].Name != "Type") return;

            if (_brandsGrid.Rows[e.RowIndex].DataBoundItem is not ICarBrand currentBrand) return;

            var selectedValue = _brandsGrid.Rows[e.RowIndex].Cells["Type"].Value;
            if (selectedValue == null) return;

            var newType = (CarType)Enum.Parse(typeof(CarType), selectedValue.ToString()!);
            if (currentBrand.Type == newType) return;

            var replacement = CarBrandBase.CloneWithType(currentBrand, newType);
            _brands[e.RowIndex] = replacement;
            _bindingSource.ResetBindings(false);
        }

        private void BrandsGrid_SelectionChanged(object? sender, EventArgs e)
        {
            if (_brandsGrid.SelectedRows.Count == 0)
            {
                _carsGrid.Rows.Clear();
                _carsGrid.Columns.Clear();
                return;
            }

            var selectedRow = _brandsGrid.SelectedRows[0];
            if (selectedRow.DataBoundItem is not ICarBrand brand) return;

            selectedRow.Tag = brand;
            LoadCarsForBrand(brand);
        }

        private void LoadCarsForBrand(ICarBrand brand)
        {
            _carsGrid.Rows.Clear();
            _carsGrid.Columns.Clear();

            if (brand.Type == CarType.Passenger)
            {
                _carsGrid.Columns.Add("RegNumber", "Регистрационный номер");
                _carsGrid.Columns.Add("Multimedia", "Мультимедиа");
                _carsGrid.Columns.Add("Airbags", "Подушек безопасности");
            }
            else
            {
                _carsGrid.Columns.Add("RegNumber", "Регистрационный номер");
                _carsGrid.Columns.Add("WheelCount", "Кол-во колес");
                _carsGrid.Columns.Add("BodyVolume", "Объем кузова (м³)");
            }

            _progressBar.Value = 0;
            _progressTimer.Start();

            _loadingThread = new Thread(() =>
            {
                Loader.Load(brand);
                BeginInvoke(new Action(() =>
                {
                    foreach (var car in Loader.GetCars(brand))
                    {
                        if (brand.Type == CarType.Passenger && car is PassengerCarInstance passenger)
                        {
                            _carsGrid.Rows.Add(passenger.RegistrationNumber, passenger.MultimediaName, passenger.AirbagCount);
                        }
                        else if (brand.Type == CarType.Truck && car is TruckInstance truck)
                        {
                            _carsGrid.Rows.Add(truck.RegistrationNumber, truck.WheelCount, truck.BodyVolume);
                        }
                    }

                    _progressTimer.Stop();
                    _progressBar.Value = 100;
                }));
            })
            { IsBackground = true };

            _loadingThread.Start();
        }

        private void SaveMenu_Click(object? sender, EventArgs e)
        {
            using var saveDialog = new SaveFileDialog
            {
                Filter = "XML files (*.xml)|*.xml",
                Title = "Сохранить список марок"
            };

            if (saveDialog.ShowDialog() != DialogResult.OK) return;

            try
            {
                var data = _brands.OfType<CarBrandBase>().Select(b => CarBrandBase.CloneWithType(b, b.Type)).ToList();
                using var file = new FileStream(saveDialog.FileName, FileMode.Create);
                BrandSerializer.Serialize(file, data);
                MessageBox.Show("Список марок сохранён.", "Успех", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadMenu_Click(object? sender, EventArgs e)
        {
            using var openDialog = new OpenFileDialog
            {
                Filter = "XML files (*.xml)|*.xml",
                Title = "Загрузить список марок"
            };

            if (openDialog.ShowDialog() != DialogResult.OK) return;

            try
            {
                using var file = new FileStream(openDialog.FileName, FileMode.Open);
                if (BrandSerializer.Deserialize(file) is List<CarBrandBase> loaded)
                {
                    _brands = new BindingList<ICarBrand>(loaded.Cast<ICarBrand>().ToList());
                    _bindingSource.DataSource = _brands;
                    _bindingSource.ResetBindings(false);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (_loadingThread != null && _loadingThread.IsAlive)
            {
                _loadingThread.Join(200);
            }
        }
    }
}

