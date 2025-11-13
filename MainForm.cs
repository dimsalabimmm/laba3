using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Serialization;
using System.IO;

namespace Laba3
{
    public partial class MainForm : Form
    {
        private BindingSource bindingSource;
        private DataGridView brandsGridView;
        private DataGridView carsGridView;
        private ProgressBar progressBar;
        private Timer progressTimer;
        private MenuStrip menuStrip;
        private Thread loadingThread;

        public MainForm()
        {
            InitializeComponent();
            InitializeData();
        }

        private void InitializeComponent()
        {
            this.Text = "Car Brands Management";
            this.Size = new Size(1000, 600);
            this.StartPosition = FormStartPosition.CenterScreen;

            // Menu Strip
            menuStrip = new MenuStrip();
            ToolStripMenuItem fileMenu = new ToolStripMenuItem("File");
            ToolStripMenuItem saveItem = new ToolStripMenuItem("Save list of brands");
            ToolStripMenuItem loadItem = new ToolStripMenuItem("Load");
            ToolStripMenuItem exitItem = new ToolStripMenuItem("Exit");

            saveItem.Click += SaveItem_Click;
            loadItem.Click += LoadItem_Click;
            exitItem.Click += ExitItem_Click;

            fileMenu.DropDownItems.Add(saveItem);
            fileMenu.DropDownItems.Add(loadItem);
            fileMenu.DropDownItems.Add(exitItem);
            menuStrip.Items.Add(fileMenu);
            this.MainMenuStrip = menuStrip;
            this.Controls.Add(menuStrip);

            // Brands DataGridView
            brandsGridView = new DataGridView
            {
                Dock = DockStyle.Left,
                Width = 500,
                AllowUserToAddRows = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                ReadOnly = false,
                AutoGenerateColumns = false
            };

            brandsGridView.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "BrandName",
                HeaderText = "Brand Name",
                DataPropertyName = "BrandName"
            });
            brandsGridView.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ModelName",
                HeaderText = "Model Name",
                DataPropertyName = "ModelName"
            });
            brandsGridView.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Horsepower",
                HeaderText = "Horsepower",
                DataPropertyName = "Horsepower"
            });
            brandsGridView.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "MaxSpeed",
                HeaderText = "Max Speed",
                DataPropertyName = "MaxSpeed"
            });
            
            DataGridViewComboBoxColumn typeColumn = new DataGridViewComboBoxColumn
            {
                Name = "Type",
                HeaderText = "Type",
                DataSource = Enum.GetValues(typeof(CarType))
            };
            typeColumn.DisplayMember = "ToString";
            typeColumn.ValueMember = "ToString";
            brandsGridView.Columns.Add(typeColumn);

            brandsGridView.SelectionChanged += BrandsGridView_SelectionChanged;
            brandsGridView.CellValueChanged += BrandsGridView_CellValueChanged;
            brandsGridView.CellFormatting += BrandsGridView_CellFormatting;
            brandsGridView.RowsAdded += BrandsGridView_RowsAdded;
            brandsGridView.DataError += BrandsGridView_DataError;

            // Cars DataGridView
            carsGridView = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };

            // Progress Bar
            progressBar = new ProgressBar
            {
                Dock = DockStyle.Bottom,
                Height = 30,
                Style = ProgressBarStyle.Continuous
            };

            // Progress Timer
            progressTimer = new Timer
            {
                Interval = 100
            };
            progressTimer.Tick += ProgressTimer_Tick;

            // Split Container
            SplitContainer splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                SplitterDistance = 500
            };
            splitContainer.Panel1.Controls.Add(brandsGridView);
            splitContainer.Panel2.Controls.Add(carsGridView);
            splitContainer.Panel2.Controls.Add(progressBar);

            this.Controls.Add(splitContainer);
        }

        private void InitializeData()
        {
            bindingSource = new BindingSource();
            bindingSource.DataSource = new List<CarBrand>();
            brandsGridView.DataSource = bindingSource;
        }

        private void BrandsGridView_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            // Suppress data binding errors
            e.ThrowException = false;
        }

        private void BrandsGridView_RowsAdded(object sender, DataGridViewRowsAddedEventArgs e)
        {
            // When a new row is added, ensure it has a bound object
            for (int i = 0; i < e.RowCount; i++)
            {
                int rowIndex = e.RowIndex + i;
                if (rowIndex < brandsGridView.Rows.Count)
                {
                    var row = brandsGridView.Rows[rowIndex];
                    if (row.DataBoundItem == null && rowIndex == brandsGridView.Rows.Count - 1)
                    {
                        // This is the new row - create a default PassengerCar
                        var newBrand = new PassengerCar
                        {
                            BrandName = "",
                            ModelName = "",
                            Horsepower = 0,
                            MaxSpeed = 0
                        };
                        bindingSource.Add(newBrand);
                    }
                }
            }
        }

        private void BrandsGridView_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (e.RowIndex >= brandsGridView.Rows.Count) return;

            var row = brandsGridView.Rows[e.RowIndex];
            var brand = row.DataBoundItem as CarBrand;
            
            // Handle new row addition - update values if brand exists
            if (brand != null)
            {
                // Update brand properties from cell values
                if (e.ColumnIndex == brandsGridView.Columns["BrandName"].Index)
                {
                    brand.BrandName = row.Cells["BrandName"].Value?.ToString() ?? "";
                }
                else if (e.ColumnIndex == brandsGridView.Columns["ModelName"].Index)
                {
                    brand.ModelName = row.Cells["ModelName"].Value?.ToString() ?? "";
                }
                else if (e.ColumnIndex == brandsGridView.Columns["Horsepower"].Index)
                {
                    int.TryParse(row.Cells["Horsepower"].Value?.ToString(), out int hp);
                    brand.Horsepower = hp;
                }
                else if (e.ColumnIndex == brandsGridView.Columns["MaxSpeed"].Index)
                {
                    int.TryParse(row.Cells["MaxSpeed"].Value?.ToString(), out int speed);
                    brand.MaxSpeed = speed;
                }
            }

            if (brand == null) return;

            // Handle type change
            if (e.ColumnIndex == brandsGridView.Columns["Type"].Index)
            {
                var newTypeValue = row.Cells["Type"].Value?.ToString();
                if (Enum.TryParse<CarType>(newTypeValue, out CarType newType))
                {
                    if (brand.Type != newType)
                    {
                        CarBrand newBrand;
                        if (newType == CarType.Passenger)
                        {
                            newBrand = new PassengerCar
                            {
                                BrandName = brand.BrandName,
                                ModelName = brand.ModelName,
                                Horsepower = brand.Horsepower,
                                MaxSpeed = brand.MaxSpeed
                            };
                        }
                        else
                        {
                            newBrand = new Truck
                            {
                                BrandName = brand.BrandName,
                                ModelName = brand.ModelName,
                                Horsepower = brand.Horsepower,
                                MaxSpeed = brand.MaxSpeed
                            };
                        }

                        int index = bindingSource.IndexOf(brand);
                        bindingSource.Remove(brand);
                        bindingSource.Insert(index, newBrand);
                        brandsGridView.ClearSelection();
                        if (index < brandsGridView.Rows.Count)
                        {
                            brandsGridView.Rows[index].Selected = true;
                        }
                    }
                }
            }
        }

        private void BrandsGridView_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;

            var row = brandsGridView.Rows[e.RowIndex];
            var brand = row.DataBoundItem as CarBrand;
            if (brand == null) return;

            // Set Type column value
            if (e.ColumnIndex == brandsGridView.Columns["Type"].Index)
            {
                e.Value = brand.Type.ToString();
            }

            // Set row color based on type
            if (brand.Type == CarType.Passenger)
            {
                row.DefaultCellStyle.BackColor = Color.LightBlue;
            }
            else
            {
                row.DefaultCellStyle.BackColor = Color.LightCoral;
            }
        }

        private void BrandsGridView_SelectionChanged(object sender, EventArgs e)
        {
            if (brandsGridView.SelectedRows.Count == 0)
            {
                carsGridView.Rows.Clear();
                return;
            }

            var selectedRow = brandsGridView.SelectedRows[0];
            var brand = selectedRow.DataBoundItem as CarBrand;
            if (brand == null) return;

            selectedRow.Tag = brand;

            LoadCarsForBrand(brand);
        }

        private void LoadCarsForBrand(CarBrand brand)
        {
            carsGridView.Rows.Clear();
            carsGridView.Columns.Clear();

            // Setup columns based on car type
            if (brand.Type == CarType.Passenger)
            {
                carsGridView.Columns.Add("RegistrationNumber", "Registration Number");
                carsGridView.Columns.Add("MultimediaName", "Multimedia Name");
                carsGridView.Columns.Add("AirbagCount", "Airbag Count");
            }
            else
            {
                carsGridView.Columns.Add("RegistrationNumber", "Registration Number");
                carsGridView.Columns.Add("WheelCount", "Number of Wheels");
                carsGridView.Columns.Add("BodyVolume", "Body Volume (m³)");
            }

            // Start loading in background thread
            progressBar.Value = 0;
            progressTimer.Start();

            loadingThread = new Thread(() =>
            {
                Loader.Load(brand);
                this.Invoke(new Action(() =>
                {
                    var cars = Loader.GetCars(brand);
                    foreach (var car in cars)
                    {
                        if (car is PassengerCarInstance passengerCar)
                        {
                            carsGridView.Rows.Add(
                                passengerCar.RegistrationNumber,
                                passengerCar.MultimediaName,
                                passengerCar.AirbagCount
                            );
                        }
                        else if (car is TruckInstance truck)
                        {
                            carsGridView.Rows.Add(
                                truck.RegistrationNumber,
                                truck.WheelCount,
                                truck.BodyVolume
                            );
                        }
                    }
                    progressTimer.Stop();
                    progressBar.Value = 100;
                }));
            });
            loadingThread.Start();
        }

        private void ProgressTimer_Tick(object sender, EventArgs e)
        {
            int progress = Loader.GetProgress();
            progressBar.Value = Math.Min(progress, 100);
        }

        private void SaveItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog saveDialog = new SaveFileDialog
            {
                Filter = "XML files (*.xml)|*.xml",
                DefaultExt = "xml"
            };

            if (saveDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    var brands = bindingSource.Cast<CarBrand>().ToList();
                    XmlSerializer serializer = new XmlSerializer(typeof(List<CarBrand>), new[] { typeof(PassengerCar), typeof(Truck) });
                    using (FileStream stream = new FileStream(saveDialog.FileName, FileMode.Create))
                    {
                        serializer.Serialize(stream, brands);
                    }
                    MessageBox.Show("Brands saved successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error saving: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void LoadItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog openDialog = new OpenFileDialog
            {
                Filter = "XML files (*.xml)|*.xml"
            };

            if (openDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(List<CarBrand>), new[] { typeof(PassengerCar), typeof(Truck) });
                    List<CarBrand> brands;
                    using (FileStream stream = new FileStream(openDialog.FileName, FileMode.Open))
                    {
                        brands = (List<CarBrand>)serializer.Deserialize(stream);
                    }
                    bindingSource.DataSource = brands;
                    brandsGridView.Refresh();
                    brandsGridView.Invalidate();
                    MessageBox.Show("Brands loaded successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ExitItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }
    }
}

