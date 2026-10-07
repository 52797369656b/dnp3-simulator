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

        public static void Restore(Control root, string section, IReadOnlyDictionary<string, string> values)
        {
            RestoreChildren(root, section, values);
        }

        public static T RestoreObject<T>(string prefix, IReadOnlyDictionary<string, string> values) where T : new()
        {
            var instance = new T();
            PopulateObject(instance!, prefix, values);
            return instance;
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

        private static void RestoreChildren(Control parent, string parentPath, IReadOnlyDictionary<string, string> values)
        {
            foreach (Control control in parent.Controls)
            {
                var controlName = ToHeaderName(control.Name);
                var path = string.IsNullOrEmpty(controlName) ? parentPath : $"{parentPath}_{controlName}";

                switch (control)
                {
                    case TextBoxBase textBox when values.TryGetValue(path, out var text):
                        textBox.Text = text;
                        break;
                    case NumericUpDown numeric when values.TryGetValue(path, out var number) && decimal.TryParse(number, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedNumber):
                        numeric.Value = Math.Min(numeric.Maximum, Math.Max(numeric.Minimum, parsedNumber));
                        break;
                    case CheckBox checkBox when values.TryGetValue(path, out var check) && bool.TryParse(check, out var isChecked):
                        checkBox.Checked = isChecked;
                        break;
                    case RadioButton radioButton when values.TryGetValue(path, out var radio) && bool.TryParse(radio, out var isSelected):
                        radioButton.Checked = isSelected;
                        break;
                    case ComboBox comboBox when values.TryGetValue(path, out var selected):
                        RestoreComboBox(comboBox, selected);
                        break;
                    case TabControl tabControl when values.TryGetValue($"{path}_selected_tab", out var selectedTab):
                        tabControl.SelectedTab = tabControl.TabPages.Cast<TabPage>()
                            .FirstOrDefault(page => string.Equals(page.Text, selectedTab, StringComparison.OrdinalIgnoreCase));
                        break;
                    case ListView listView:
                        RestoreListView(listView, path, values);
                        break;
                    case CheckedListBox checkedListBox:
                        RestoreCheckedListBox(checkedListBox, path, values);
                        break;
                    case DateTimePicker dateTimePicker when values.TryGetValue(path, out var dateText) && DateTime.TryParse(dateText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var date):
                        dateTimePicker.Value = date;
                        break;
                }

                RestoreChildren(control, path, values);
            }
        }

        private static void RestoreComboBox(ComboBox comboBox, string value)
        {
            for (var index = 0; index < comboBox.Items.Count; index++)
            {
                if (string.Equals(comboBox.Items[index]?.ToString(), value, StringComparison.OrdinalIgnoreCase))
                {
                    comboBox.SelectedIndex = index;
                    return;
                }
            }

            comboBox.Text = value;
        }

        private static void RestoreListView(ListView listView, string path, IReadOnlyDictionary<string, string> values)
        {
            foreach (ListViewItem item in listView.Items)
            {
                var key = $"{path}_{ToHeaderName(item.Text)}";
                if (!values.TryGetValue(key, out var subItems))
                {
                    continue;
                }

                var restoredValues = subItems.Split(';');
                for (var index = 0; index < restoredValues.Length && index + 1 < item.SubItems.Count; index++)
                {
                    item.SubItems[index + 1].Text = restoredValues[index];
                }
            }
        }

        private static void RestoreCheckedListBox(CheckedListBox listBox, string path, IReadOnlyDictionary<string, string> values)
        {
            for (var index = 0; index < listBox.Items.Count; index++)
            {
                var itemName = ToHeaderName(listBox.Items[index]?.ToString() ?? index.ToString(CultureInfo.InvariantCulture));
                if (values.TryGetValue($"{path}_{itemName}", out var check) && bool.TryParse(check, out var isChecked))
                {
                    listBox.SetItemChecked(index, isChecked);
                }
            }
        }

        private static void PopulateObject(object instance, string prefix, IReadOnlyDictionary<string, string> values)
        {
            foreach (var field in instance.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                var fieldPrefix = $"{prefix}_{ToHeaderName(field.Name)}";
                var fieldValue = RestoreObjectValue(field.FieldType, fieldPrefix, values);
                if (fieldValue != null)
                {
                    field.SetValue(instance, fieldValue);
                }
            }
        }

        private static object? RestoreObjectValue(Type type, string prefix, IReadOnlyDictionary<string, string> values)
        {
            if (type == typeof(string))
            {
                return values.TryGetValue(prefix, out var text) ? text : null;
            }

            if (type.IsEnum)
            {
                return values.TryGetValue(prefix, out var text) && Enum.TryParse(type, text, true, out var parsed) ? parsed : null;
            }

            if (type == typeof(TimeSpan))
            {
                return values.TryGetValue(prefix, out var text) && TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
            }

            if (type.IsPrimitive || type == typeof(decimal))
            {
                if (!values.TryGetValue(prefix, out var text))
                {
                    return null;
                }

                try
                {
                    return Convert.ChangeType(text, type, CultureInfo.InvariantCulture);
                }
                catch (FormatException)
                {
                    return null;
                }
                catch (OverflowException)
                {
                    return null;
                }
            }

            if (typeof(IDictionary).IsAssignableFrom(type))
            {
                var dictionaryType = type.IsInterface || type.IsAbstract
                    ? typeof(Dictionary<,>).MakeGenericType(type.GetGenericArguments())
                    : type;
                var dictionary = Activator.CreateInstance(dictionaryType) as IDictionary;
                if (dictionary == null)
                {
                    return null;
                }

                var arguments = type.GetGenericArguments();
                var keyType = arguments[0];
                var valueType = arguments[1];
                var entryPrefix = $"{prefix}_";
                var entryNames = values.Keys
                    .Where(key => key.StartsWith(entryPrefix, StringComparison.OrdinalIgnoreCase))
                    .Select(key => key.Substring(entryPrefix.Length).Split('_')[0])
                    .Distinct(StringComparer.OrdinalIgnoreCase);

                foreach (var entryName in entryNames)
                {
                    object entryKey;
                    try
                    {
                        entryKey = Convert.ChangeType(entryName, keyType, CultureInfo.InvariantCulture);
                    }
                    catch (FormatException)
                    {
                        continue;
                    }

                    var entryValue = RestoreObjectValue(valueType, $"{prefix}_{entryName}", values);
                    if (entryValue != null)
                    {
                        dictionary[entryKey] = entryValue;
                    }
                }

                return dictionary;
            }

            var instance = Activator.CreateInstance(type);
            if (instance != null)
            {
                PopulateObject(instance, prefix, values);
            }
            return instance;
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