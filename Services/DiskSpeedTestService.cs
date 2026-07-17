using System;
using System.IO;
using System.Diagnostics;

namespace atp_enterprise_app_wpf.Services
{
    public class DiskSpeedResult
    {
        public double WriteSpeedMBs { get; set; }
        public double ReadSpeedMBs { get; set; }
        public double WriteTimeSec { get; set; }
        public double ReadTimeSec { get; set; }
        public bool IntegrityChecked { get; set; }
    }

    public class DiskSpeedTestService
    {
        public DiskSpeedResult RunBenchmark(string targetFolder)
        {
            string filePath = Path.Combine(targetFolder, "atp_disk_bench.tmp");
            int sizeInBytes = 20 * 1024 * 1024; 
            byte[] writeBuffer = new byte[sizeInBytes];
            
            for (int i = 0; i < writeBuffer.Length; i++)
            {
                writeBuffer[i] = (byte)(i % 256);
            }

            var result = new DiskSpeedResult();

            try
            {
                if (!Directory.Exists(targetFolder))
                    Directory.CreateDirectory(targetFolder);

                Stopwatch sw = Stopwatch.StartNew();
                using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    fs.Write(writeBuffer, 0, writeBuffer.Length);
                }
                sw.Stop();
                
                result.WriteTimeSec = sw.Elapsed.TotalSeconds;
                result.WriteSpeedMBs = Math.Round((20.0) / result.WriteTimeSec, 2);

                byte[] readBuffer = new byte[sizeInBytes];
                sw.Restart();
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    fs.Read(readBuffer, 0, readBuffer.Length);
                }
                sw.Stop();

                result.ReadTimeSec = sw.Elapsed.TotalSeconds;
                result.ReadSpeedMBs = Math.Round((20.0) / result.ReadTimeSec, 2);

                bool match = true;
                for (int i = 0; i < sizeInBytes; i++)
                {
                    if (readBuffer[i] != writeBuffer[i])
                    {
                        match = false;
                        break;
                    }
                }
                result.IntegrityChecked = match;
            }
            catch (Exception)
            {
                result.IntegrityChecked = false;
                result.ReadSpeedMBs = 0;
                result.WriteSpeedMBs = 0;
            }
            finally
            {
                try
                {
                    if (File.Exists(filePath))
                        File.Delete(filePath);
                }
                catch {}
            }

            return result;
        }
    }
}
