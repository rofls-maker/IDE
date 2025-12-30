using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace IDE
{
    public static class NativeMethods
    {
        private const string DllName = "editor_core.dll";

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool open_file(byte[] path);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool save_file(byte[] path);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void close_file();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool is_valid_state();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern UIntPtr get_line_count();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr get_line_content(UIntPtr lineIdx);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool replace_line(UIntPtr lineIdx, byte[] newText);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool has_line_error(UIntPtr lineIdx);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr get_line_error_message(UIntPtr lineIdx);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr get_all_errors();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr compile_c_file(byte[] path, byte[] outputPath);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool test_connection();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void free_string(IntPtr ptr);

        public static bool OpenFileSafe(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
                    return false;

                byte[] pathBytes = StringToUtf8WithNull(filePath);
                return open_file(pathBytes);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка открытия файла: {ex.Message}");
                return false;
            }
        }

        public static bool TestDllConnection()
        {
            try
            {
                return test_connection();
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static string GetAllErrorsSafe()
        {
            try
            {
                if (!is_valid_state())
                    return "Файл не загружен";

                IntPtr ptr = get_all_errors();
                return GetStringFromPtr(ptr) ?? "Ошибки не найдены";
            }
            catch (Exception ex)
            {
                return $"Ошибка получения ошибок: {ex.Message}";
            }
        }

        public static string CompileCFileSafe(string sourcePath, string outputPath = null)
        {
            try
            {
                if (string.IsNullOrEmpty(sourcePath) || !System.IO.File.Exists(sourcePath))
                    return "Исходный файл не найден";

                byte[] sourceBytes = StringToUtf8WithNull(sourcePath);
                byte[] outputBytes = outputPath != null ?
                    StringToUtf8WithNull(outputPath) :
                    new byte[] { 0 };

                IntPtr resultPtr = compile_c_file(sourceBytes, outputBytes);
                return GetStringFromPtr(resultPtr) ?? "Компиляция не удалась";
            }
            catch (Exception ex)
            {
                return $"Ошибка компиляции: {ex.Message}";
            }
        }

        public static string GetStringFromPtr(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero) return null;

            try
            {
                int len = 0;
                while (Marshal.ReadByte(ptr, len) != 0)
                {
                    len++;
                    if (len > 10 * 1024 * 1024) break; 
                }

                if (len == 0) return string.Empty;

                byte[] buffer = new byte[len];
                Marshal.Copy(ptr, buffer, 0, len);

                try { free_string(ptr); } catch { }

                return Encoding.UTF8.GetString(buffer);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка получения строки: {ex.Message}");
                return null;
            }
        }

        private static byte[] StringToUtf8WithNull(string str)
        {
            if (str == null) return new byte[] { 0 };

            byte[] utf8Bytes = Encoding.UTF8.GetBytes(str);
            byte[] result = new byte[utf8Bytes.Length + 1];
            Array.Copy(utf8Bytes, result, utf8Bytes.Length);
            result[result.Length - 1] = 0;
            return result;
        }
    }
}