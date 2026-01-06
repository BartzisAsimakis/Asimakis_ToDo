using CommunityToolkit.WinUI.Notifications; // NuGet
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace AI_ex_8
{
    public partial class MainWindow : Window
    {
        // ====== Email settings ======
        private const string SmtpHost = "smtp.gmail.com";
        private const int SmtpPort = 587;
        private const string FromEmail = "bartzisasimakis@gmail.com";
        private const string FromName = "Task Reminder";
        private const string FromPass = "my_PASSWORD"; // APP PASSWORD από Gmail
        private const string ToEmail = "bartzisasimakis@gmail.com";

        private const string filename = "tasks.txt";

        private List<TaskItem> tasks = new List<TaskItem>();
        private readonly DispatcherTimer checkTimer = new DispatcherTimer();

        public MainWindow()
        {
            InitializeComponent();

            var gr = CultureInfo.GetCultureInfo("el-GR");
            Title = $"Λίστα Εργασιών — {DateTime.Now.ToString("d", gr)}";

            LoadTasks();
            RefreshTaskList();

            // Timer για ειδοποιήσεις
            checkTimer.Interval = TimeSpan.FromMinutes(1);
            checkTimer.Tick += CheckDueTasksAndNotify;
            checkTimer.Start();

            // Εγγραφή στο Activation handler
            ToastNotificationManagerCompat.OnActivated += ToastActivated;
        }

        // ====== Μοντέλο ======
        public class TaskItem
        {
            public string Task { get; set; }
            public DateTime Date { get; set; }
            public bool Completed { get; set; }
            public DateTime? NextReminder { get; set; }
            public bool FirstNotified { get; set; }

            public string NextReminderDisplay =>
                NextReminder.HasValue ? NextReminder.Value.ToString("dd/MM HH:mm") : "-";
        }

        // ====== Toast Activation Handler ======
        private void ToastActivated(ToastNotificationActivatedEventArgsCompat e)
        {
            var args = ToastArguments.Parse(e.Argument);
            if (args.Contains("action") && args["action"] == "complete")
            {
                string taskName = args["task"];
                var task = tasks.FirstOrDefault(t => t.Task == taskName);
                if (task != null)
                {
                    Dispatcher.Invoke(() =>
                    {
                        task.Completed = true;
                        tasks.Remove(task);
                        SaveTasks();
                        RefreshTaskList();
                    });
                }
            }
        }

        // ====== Load / Save ======
        private void LoadTasks()
        {
            tasks.Clear();
            if (!File.Exists(filename)) return;

            foreach (var line in File.ReadAllLines(filename, Encoding.UTF8))
            {
                var parts = line.Split('|');
                if (parts.Length >= 3 &&
                    DateTime.TryParse(parts[1], out DateTime dt) &&
                    int.TryParse(parts[2], out int comp))
                {
                    bool firstNotified = parts.Length >= 4 && int.TryParse(parts[3], out int fn) && fn == 1;
                    DateTime? nextRem = null;
                    if (parts.Length >= 5 && long.TryParse(parts[4], out long ticks) && ticks > 0)
                        nextRem = new DateTime(ticks);

                    tasks.Add(new TaskItem
                    {
                        Task = parts[0],
                        Date = dt.Date,
                        Completed = comp == 1,
                        FirstNotified = firstNotified,
                        NextReminder = nextRem
                    });
                }
            }
        }

        private void SaveTasks()
        {
            using (var sw = new StreamWriter(filename, false, Encoding.UTF8))
            {
                foreach (var t in tasks)
                {
                    var ticks = t.NextReminder.HasValue ? t.NextReminder.Value.Ticks.ToString() : "";
                    sw.WriteLine($"{t.Task}|{t.Date:yyyy-MM-dd}|{(t.Completed ? 1 : 0)}|{(t.FirstNotified ? 1 : 0)}|{ticks}");
                }
            }
        }

        // ====== UI Refresh ======
        private void RefreshTaskList()
        {
            toDo_ListView.Items.Clear();
            foreach (var t in tasks.OrderBy(x => x.Date).ThenBy(x => x.Task))
                toDo_ListView.Items.Add(t);
        }

        // ====== Προσθήκη ======
        private void addButton_Click(object sender, RoutedEventArgs e)
        {
            Window newWindow = new Window
            {
                Title = "Νέα Εργασία",
                Width = 420,
                Height = 320,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };

            StackPanel panel = new StackPanel { Margin = new Thickness(10) };

            TextBox taskBox = new TextBox { Width = 340, Height = 30, Margin = new Thickness(0, 0, 0, 10) };
            DatePicker datePicker = new DatePicker { SelectedDate = DateTime.Now, Width = 200, Margin = new Thickness(0, 0, 0, 10) };
            Button saveButton = new Button { Content = "Αποθήκευση", Width = 140, Height = 40 };

            panel.Children.Add(new TextBlock { Text = "Όνομα Εργασίας:" });
            panel.Children.Add(taskBox);
            panel.Children.Add(new TextBlock { Text = "Ημερομηνία Εκτέλεσης:" });
            panel.Children.Add(datePicker);
            panel.Children.Add(saveButton);

            newWindow.Content = panel;
            newWindow.Show();

            saveButton.Click += (s, args) =>
            {
                string taskName = taskBox.Text.Trim();
                if (!string.IsNullOrEmpty(taskName) && datePicker.SelectedDate.HasValue)
                {
                    DateTime selectedDate = datePicker.SelectedDate.Value.Date;
                    if (selectedDate < DateTime.Today)
                    {
                        MessageBox.Show("Η ημερομηνία δεν μπορεί να είναι πριν από σήμερα!", "Σφάλμα", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    tasks.Add(new TaskItem
                    {
                        Task = taskName,
                        Date = selectedDate,
                        Completed = false,
                        FirstNotified = false,
                        NextReminder = null
                    });
                    newWindow.Close();
                    SaveTasks();
                    RefreshTaskList();
                }
                else
                {
                    MessageBox.Show("Συμπλήρωσε και όνομα και ημερομηνία.", "Σφάλμα", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            };
        }

        // ====== Ολοκληρώθηκε (χειροκίνητα από UI) ======
        private void completeButton_Click(object sender, RoutedEventArgs e)
        {
            if (toDo_ListView.SelectedItem is TaskItem selected)
            {
                selected.Completed = true;
                tasks.Remove(selected);
                SaveTasks();
                RefreshTaskList();
            }
            else
            {
                MessageBox.Show("Επίλεξε μία εργασία για ολοκλήρωση.", "Προσοχή", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // ====== Επεξεργασία ======
        private void editButton_Click(object sender, RoutedEventArgs e)
        {
            if (toDo_ListView.SelectedItem is TaskItem selected)
            {
                // Νέο παράθυρο για επεξεργασία
                Window editWindow = new Window
                {
                    Title = "Επεξεργασία Εργασίας",
                    Width = 420,
                    Height = 320,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen
                };

                StackPanel panel = new StackPanel { Margin = new Thickness(10) };

                TextBox taskBox = new TextBox
                {
                    Width = 340,
                    Height = 30,
                    Margin = new Thickness(0, 0, 0, 10),
                    Text = selected.Task // προσυμπληρωμένο
                };

                DatePicker datePicker = new DatePicker
                {
                    SelectedDate = selected.Date,
                    Width = 200,
                    Margin = new Thickness(0, 0, 0, 10)
                };

                Button saveButton = new Button
                {
                    Content = "Αποθήκευση Αλλαγών",
                    Width = 160,
                    Height = 40
                };

                panel.Children.Add(new TextBlock { Text = "Όνομα Εργασίας:" });
                panel.Children.Add(taskBox);
                panel.Children.Add(new TextBlock { Text = "Ημερομηνία Εκτέλεσης:" });
                panel.Children.Add(datePicker);
                panel.Children.Add(saveButton);

                editWindow.Content = panel;
                editWindow.Show();

                saveButton.Click += (s, args) =>
                {
                    string newTaskName = taskBox.Text.Trim();
                    if (!string.IsNullOrEmpty(newTaskName) && datePicker.SelectedDate.HasValue)
                    {
                        DateTime selectedDate = datePicker.SelectedDate.Value.Date;
                        if (selectedDate < DateTime.Today)
                        {
                            MessageBox.Show("Η ημερομηνία δεν μπορεί να είναι πριν από σήμερα!",
                                "Σφάλμα", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }

                        // Ενημέρωση της υπάρχουσας εργασίας
                        selected.Task = newTaskName;
                        selected.Date = selectedDate;

                        SaveTasks();
                        RefreshTaskList();
                        editWindow.Close();
                    }
                    else
                    {
                        MessageBox.Show("Συμπλήρωσε και όνομα και ημερομηνία.",
                            "Σφάλμα", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                };
            }
            else
            {
                MessageBox.Show("Επίλεξε μία εργασία για επεξεργασία.",
                    "Προσοχή", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }


        // ====== Κατάργηση ======
        private void removeButton_Click(object sender, RoutedEventArgs e)
        {
            if (toDo_ListView.SelectedItem is TaskItem selected)
            {
                tasks.Remove(selected);
                SaveTasks();
                RefreshTaskList();
            }
        }

        // ====== Exit ======
        private void exitButton_Click(object sender, RoutedEventArgs e)
        {
            SaveTasks();
            Close();
        }

        // ====== Λογική ειδοποιήσεων ======
        private void CheckDueTasksAndNotify(object sender, EventArgs e)
        {
            var now = DateTime.Now;

            foreach (var t in tasks.ToList())
            {
                if (t.Completed) continue;
                if (t.Date.Date > DateTime.Today) continue;

                if (!t.FirstNotified && t.Date.Date <= DateTime.Today)
                {
                    SendAllNotifications(t);
                    t.FirstNotified = true;
                    t.NextReminder = now.AddHours(6);
                    SaveTasks();
                    RefreshTaskList();
                    continue;
                }

                if (t.NextReminder.HasValue && now >= t.NextReminder.Value)
                {
                    SendAllNotifications(t);
                    t.NextReminder = now.AddHours(6);
                    SaveTasks();
                    RefreshTaskList();
                }
            }
        }

        private void SendAllNotifications(TaskItem t)
        {
            try { ShowToast(t); } catch { }
            try { SendEmail(t); }
            catch (Exception ex)
            {
                MessageBox.Show($"Σφάλμα αποστολής email: {ex.Message}", "Email", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // ====== Toast ======
        private void ShowToast(TaskItem t)
        {
            new ToastContentBuilder()
                .AddText("Εργασία προς εκτέλεση")
                .AddText($"{t.Task} — {t.Date:dd/MM/yyyy}")
                .AddButton(new ToastButton()
                    .SetContent("Ολοκληρώθηκε")
                    .AddArgument("action", "complete")
                    .AddArgument("task", t.Task))
                .AddButton(new ToastButtonDismiss("Αργότερα"))
                .Show();
        }

        // ====== Email ======
        private void SendEmail(TaskItem t)
        {
            using (var client = new SmtpClient(SmtpHost, SmtpPort))
            {
                client.EnableSsl = true;
                client.Credentials = new NetworkCredential(FromEmail, FromPass);

                var msg = new MailMessage
                {
                    From = new MailAddress(FromEmail, FromName, Encoding.UTF8),
                    Subject = $"Υπενθύμιση εργασίας: {t.Task}",
                    Body = $"Εργασία: {t.Task}\r\nΗμερομηνία: {t.Date:dd/MM/yyyy}\r\n\r\n" +
                           $"Η εργασία παραμένει σε εκκρεμότητα. " +
                           $"Αν δεν ολοκληρωθεί, θα λάβεις νέα υπενθύμιση σε 6 ώρες.",
                    BodyEncoding = Encoding.UTF8,
                    SubjectEncoding = Encoding.UTF8
                };
                msg.To.Add(ToEmail);
                client.Send(msg);
            }
        }
    }
}
