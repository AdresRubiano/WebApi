namespace WebApi.Services;

/// <summary>
/// Interfaz para el servicio de almacenamiento en Amazon S3
/// </summary>
public interface IS3Service
{
    /// <summary>
    /// Sube un archivo a S3
    /// </summary>
    /// <param name="fileStream">Stream del archivo a subir</param>
    /// <param name="fileName">Nombre del archivo original</param>
    /// <param name="contentType">Tipo MIME del archivo</param>
    /// <returns>URL pública del archivo subido</returns>
    Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType);

    /// <summary>
    /// Elimina un archivo de S3
    /// </summary>
    /// <param name="fileUrl">URL completa del archivo en S3</param>
    /// <returns>True si se eliminó correctamente, False en caso contrario</returns>
    Task<bool> DeleteFileAsync(string fileUrl);

    /// <summary>
    /// Valida si un archivo es una imagen válida
    /// </summary>
    /// <param name="file">Archivo a validar</param>
    /// <returns>True si es válido, False en caso contrario</returns>
    bool IsValidImageFile(IFormFile file);

    /// <summary>
    /// Valida si una URL de imagen es válida y pertenece al bucket configurado
    /// </summary>
    /// <param name="imageUrl">URL de la imagen a validar</param>
    /// <returns>True si es válida, False en caso contrario</returns>
    bool IsValidImageUrl(string? imageUrl);

    /// <summary>
    /// Verifica si una imagen existe en S3
    /// </summary>
    /// <param name="imageUrl">URL de la imagen a verificar</param>
    /// <returns>True si existe, False en caso contrario</returns>
    Task<bool> ImageExistsAsync(string? imageUrl);
}

