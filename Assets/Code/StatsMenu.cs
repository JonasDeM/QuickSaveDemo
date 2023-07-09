// Author: Jonas De Maeseneer

using System.Text;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

namespace QuickSaveDemo
{
    public class StatsMenu : MonoBehaviour
    {
        private StatsSystem _statisticsSystem;
        private StringBuilder _stringBuilder;
        private bool _displayStats;

        [SerializeField] private Text _textComponent;

        private void Start()
        {
            _statisticsSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<StatsSystem>();
            _stringBuilder = new StringBuilder(128);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                _displayStats = !_displayStats;
            }
            
            var stats = _statisticsSystem.Statistics;
            _stringBuilder.Clear();

            if (_displayStats)
            {
                _stringBuilder.Append($"Total container memory: ");
                _stringBuilder.Append(GetBytesReadable(stats.TotalByteCount));
                _stringBuilder.Append(" (No Compression)");
                _stringBuilder.AppendLine();
                
                _stringBuilder.Append($"Amount tracked entities: ");
                _stringBuilder.Append(stats.TotalTrackedEntities);
                _stringBuilder.AppendLine();
            
                _stringBuilder.Append($"Amount containers: ");
                _stringBuilder.Append(stats.TotalAmountContainers);
                _stringBuilder.AppendLine();
            
                _stringBuilder.Append($"Amount unique container ids: ");
                _stringBuilder.Append(stats.TotalAmountUniqueContainerIds);
                _stringBuilder.AppendLine();
            }

            _textComponent.text = _stringBuilder.ToString();
        }
        
        // Returns the human-readable file size for an arbitrary, 64-bit file size 
        // The default format is "0.### XB", e.g. "4.2 KB" or "1.434 GB"
        public string GetBytesReadable(long bytesSigned)
        {
            // Get absolute value
            long bytes = (bytesSigned < 0 ? -bytesSigned : bytesSigned);
            // Determine the suffix and readable value
            string suffix;
            double readable;
            if (bytes >= 0x40000000) // Gigabyte
            {
                suffix = "GB";
                readable = (bytesSigned >> 20);
            }
            else if (bytes >= 0x100000) // Megabyte
            {
                suffix = "MB";
                readable = (bytesSigned >> 10);
            }
            else if (bytes >= 0x400) // Kilobyte
            {
                suffix = "KB";
                readable = bytesSigned;
            }
            else
            {
                return bytesSigned.ToString("0 B"); // Byte
            }
            // Divide by 1024 to get fractional value
            readable = (readable / 1024);
            // Return formatted number with suffix
            return readable.ToString("0.## ") + suffix;
        }
    }
}