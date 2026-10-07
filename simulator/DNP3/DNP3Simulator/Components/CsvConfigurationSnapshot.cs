using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace Automatak.Simulator.DNP3.Components
{
    internal static class CsvConfigurationSnapshot
    {
        public static Dictionary<string, string> Capture(Control root, string section)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            CaptureChildren(root, section, values);
            return values;
        }

        public static void AddObjectValues(Dictionary<string, string> values, string prefix, object? instance)
        {
            AddObjectValues(values, prefix, instance, 0);
        }

        private static void AddObjectValues(Dictionary<string, string> values, string prefix, object? instance, int depth)
        {
            if (instance == null || depth > 8)
            {
                return;
            }

            var type = instance.GetType();
            if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal) || type == typeof(TimeSpan))
            {
                values[prefix] = Convert.ToString(instance, CultureInfo.InvariantCulture) ?? string.Empty;
                return;
            }

            if (instance is IDictionary dictionary)
            {
                foreach (DictionaryEntry entry in dictionary)
                {
                    var key = ToHeaderName(Convert.ToString(entry.Key, CultureInfo.InvariantCulture) ?? string.Empty);
                    AddObjectValues(values, $"{prefix}_{key}", entry.Value, depth + 1);
                }
                return;
            }

            if (instance is IEnumerable sequence)
            {
                var index = 0;
                foreach (var item in sequence)
                {
                    AddObjectValues(values, $"{prefix}_{index}", item, depth + 1);
                    index++;
                }
                return;
            }

            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                AddObjectValues(values, $"{prefix}_{ToHeaderName(field.Name)}", field.GetValue(instance), depth + 1);
            }
        }

        private static void CaptureChildren(Control parent, string parentPath, Dictionary<string, string> values)
        {
            foreach (Control control in parent.Controls)
            {
                var controlName = ToHeaderName(control.Name);
                var path = string.IsNullOrEmpty(controlName) ? parentPath : $"{parentPath}_{controlName}";

                switch (control)
                {
                    case TextBoxBase textBox:
                        values[path] = textBox.Text;
                        break;
                    case NumericUpDown numeric:
                        values[path] = numeric.Value.ToString(CultureInfo.InvariantCulture);
                        break;
                    case CheckBox checkBox:
                        values[path] = checkBox.Checked.ToString();
                        break;
                    case RadioButton radioButton:
                        values[path] = radioButton.Checked.ToString();
                        break;
                    case ComboBox comboBox:
                        values[path] = comboBox.SelectedItem?.ToString() ?? comboBox.Text;
                        break;
                    case TabControl tabControl:
                        values[$"{path}_selected_tab"] = tabControl.SelectedTab?.Text ?? string.Empty;
                        break;
                    case ListView listView:
                        CaptureListView(listView, path, values);
                        break;
                    case CheckedListBox checkedListBox:
                        CaptureCheckedListBox(checkedListBox, path, values);
                        break;
                    case ListBox listBox:
                        values[path] = string.Join(";", listBox.SelectedItems.Cast<object>().Select(item => item.ToString()));
                        break;
                    case DateTimePicker dateTimePicker:
                        values[path] = dateTimePicker.Value.ToString("O", CultureInfo.InvariantCulture);
                        break;
                }

                CaptureChildren(control, path, values);
            }
        }

        private static void CaptureListView(ListView listView, string path, Dictionary<string, string> values)
        {
            foreach (ListViewItem item in listView.Items)
            {
                var itemName = ToHeaderName(item.Text);
                if (itemName.Length == 0)
                {
                    continue;
                }

                var itemValues = item.SubItems.Cast<ListViewItem.ListViewSubItem>()
                    .Skip(1)
                    .Select(subItem => subItem.Text);
                values[$"{path}_{itemName}"] = string.Join(";", itemValues);
            }
        }

        private static void CaptureCheckedListBox(CheckedListBox listBox, string path, Dictionary<string, string> values)
        {
            for (var index = 0; index < listBox.Items.Count; index++)
            {
                var itemName = ToHeaderName(listBox.Items[index]?.ToString() ?? index.ToString(CultureInfo.InvariantCulture));
                values[$"{path}_{itemName}"] = listBox.GetItemChecked(index).ToString();
            }
        }

        private static string ToHeaderName(string value)
        {
            var result = new StringBuilder(value.Length + 8);
            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                if (!char.IsLetterOrDigit(character))
                {
                    if (result.Length > 0 && result[result.Length - 1] != '_')
                    {
                        result.Append('_');
                    }
                    continue;
                }

                if (char.IsUpper(character) && index > 0 &&
                    (char.IsLower(value[index - 1]) || char.IsDigit(value[index - 1])) &&
                    result[result.Length - 1] != '_')
                {
                    result.Append('_');
                }

                result.Append(char.ToLowerInvariant(character));
            }

            return result.ToString().Trim('_');
        }
    }
}