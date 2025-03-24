using Avalonia.Controls;
using Avalonia.Platform.Storage;
using System.IO;
using System.Threading.Tasks;

namespace Project.Classes
{
    public static class HelperLocalFile
    {
        /// <summary>
        /// Метод для выбора локального файла с заданным расширением и получения его потока
        /// </summary>
        /// <param name="extension">Расширение файла для выбора (например, "txt" или "jpg")</param>
        /// <returns>Поток выбранного файла</returns>
        public static async Task<Stream> GetFileStream(string extension)
        {
            var topLevel = TopLevel.GetTopLevel(Buffer.MainControl);
            Stream stream = null;

            if (topLevel != null)
            {
                var storageProvider = topLevel.StorageProvider;

                var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    AllowMultiple = false,
                    FileTypeFilter = new[]
                    {
                new FilePickerFileType($"Файлы {extension}")
                {
                    Patterns = new[] { $"*.{extension}" }
                },
                new FilePickerFileType("Все файлы")
                {
                    Patterns = new[] { "*.*" }
                }
            }
                });

                if (files != null && files.Count > 0)
                {
                    var file = files[0];
                    // Открываем поток для чтения файла
                    stream = await file.OpenReadAsync();
                }
            }

            return stream;
        }

        /// <summary>
        /// Метод для выбора пути сохранения файла и получения потока для записи
        /// </summary>
        /// <param name="fileName">Предложенное имя файла</param>
        /// <returns>Объект IStorageFile выбранного файла для сохранения</returns>
        public static async Task<IStorageFile> GetSaveFile(string fileName)
        {
            var topLevel = TopLevel.GetTopLevel(Buffer.MainControl);
            IStorageFile chosenFile = null;

            if (topLevel != null)
            {
                var storageProvider = topLevel.StorageProvider;

                var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    SuggestedFileName = fileName, // Предложенное имя файла по умолчанию
                    FileTypeChoices = new[]
                    {
                new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
            }
                });

                if (file != null)
                {
                    chosenFile = file;
                }
            }

            return chosenFile;
        }
    }
}
