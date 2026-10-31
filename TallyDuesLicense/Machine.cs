// Machine identity: four salted hashes. Only hashes ever leave the PC.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace TallyDues.License
{
    internal static class Machine
    {
        public static string[] Components()
        {
#if TEST_BUILD
            string fake = Environment.GetEnvironmentVariable("TD_TEST_MACHINE");
            if (!string.IsNullOrEmpty(fake))
            {
                return fake.Split(',').Select(part => Sha256Hex(part)).ToArray();
            }
#endif
            string guid = MachineGuid();
            return new[]
            {
                Component("guid", guid, guid),
                Component("cpu", Wmi("Win32_Processor", "ProcessorId"), guid),
                Component("board", Wmi("Win32_BaseBoard", "SerialNumber"), guid),
                Component("disk", SystemDiskSerial(), guid)
            };
        }

        public static string Sha256Hex(string text)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
                return string.Concat(hash.Select(b => b.ToString("x2")));
            }
        }

        // A nonce tied to the machine: another PC cannot reuse a stored signed response.
        public static string Nonce(string[] components, string random)
        {
            string[] sorted = components.OrderBy(c => c, StringComparer.Ordinal).ToArray();
            return Sha256Hex(string.Join("|", sorted) + "|" + random);
        }

        // The portal's rule for "same machine": at least 3 of 4 components match.
        public static bool IsSameMachine(string[] stored, string[] current)
        {
            return stored.Intersect(current).Count() >= 3;
        }

        private static string Component(string name, string value, string guid)
        {
            // Blank or placeholder serials fall back to the Windows ID, so such PCs never match each other.
            string usable = IsUsable(value) ? value.Trim() : "missing:" + guid;
            return Sha256Hex(Config.MachineSalt + "|" + name + "|" + usable);
        }

        private static bool IsUsable(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            string v = value.Trim().ToLowerInvariant();
            string[] placeholders = { "default string", "none", "not applicable", "system serial number", "unknown", "0", "123456789" };
            return !placeholders.Contains(v) && !v.StartsWith("to be filled") && v.Trim('0', '-', ' ').Length > 0;
        }

        private static string MachineGuid()
        {
            using (RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            using (RegistryKey key = hklm.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography"))
            {
                object value = key == null ? null : key.GetValue("MachineGuid");
                return value == null ? "no-machine-guid" : value.ToString();
            }
        }

        private static string Wmi(string table, string property)
        {
            return Query("SELECT " + property + " FROM " + table, property).FirstOrDefault(IsUsable) ?? "";
        }

        private static string SystemDiskSerial()
        {
            try
            {
                string drive = Query("SELECT SystemDrive FROM Win32_OperatingSystem", "SystemDrive").FirstOrDefault();
                foreach (string partition in Query("ASSOCIATORS OF {Win32_LogicalDisk.DeviceID='" + drive + "'} WHERE AssocClass=Win32_LogicalDiskToPartition", "DeviceID"))
                {
                    foreach (string serial in Query("ASSOCIATORS OF {Win32_DiskPartition.DeviceID='" + partition + "'} WHERE AssocClass=Win32_DiskDriveToDiskPartition", "SerialNumber"))
                    {
                        if (IsUsable(serial)) return serial;
                    }
                }
            }
            catch (Exception)
            {
                // A blank serial falls back to the Windows ID.
            }
            return "";
        }

        private static List<string> Query(string query, string property)
        {
            var values = new List<string>();
            try
            {
                using (var searcher = new ManagementObjectSearcher(query))
                using (ManagementObjectCollection results = searcher.Get())
                {
                    foreach (ManagementBaseObject item in results)
                    {
                        object value = item[property];
                        if (value != null) values.Add(value.ToString());
                    }
                }
            }
            catch (Exception)
            {
                // WMI can be blocked; the caller treats no values as a blank serial.
            }
            return values;
        }
    }
}
