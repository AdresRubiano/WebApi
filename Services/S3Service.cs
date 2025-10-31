using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using System.Text.RegularExpressions;

namespace WebApi.Services
{
    /// <summary>
    /// Servicio para interactuar con Amazon S3 siguiendo mejores prácticas
    /// </summary>
    public class S3Service : IS3Service
    {
        private readonly IAmazonS3 _s3Client;
        private readonly string _bucketName;
        private readonly string _region;
        private readonly long _maxFileSize;
        private readonly string[] _allowedImageTypes = {
            "image/jpeg",
            "image/jpg",
            "image/png",
            "image/gif",
            "image/webp"
        };
        private readonly string[] _allowedExtensions = {
            ".jpg",
            ".jpeg",
            ".png",
            ".gif",
            ".webp"
        };

        public S3Service(IConfiguration configuration)
        {
            var accessKey = configuration["AWS:AccessKey"];
            var secretKey = configuration["AWS:SecretKey"];
            _region = configuration["AWS:Region"] ?? "us-east-2";
            _bucketName = configuration["AWS:BucketName"] ?? throw new ArgumentNullException(nameof(_bucketName), "AWS BucketName no está configurado");
            
            // Tamaño máximo de archivo (10MB por defecto)
            _maxFileSize = long.Parse(configuration["AWS:MaxFileSizeMB"] ?? "10") * 1024 * 1024;

            if (string.IsNullOrWhiteSpace(accessKey) || string.IsNullOrWhiteSpace(secretKey))
            {
                throw new ArgumentNullException(nameof(accessKey), "Las credenciales de AWS no están configuradas");
            }

            var regionEndpoint = RegionEndpoint.GetBySystemName(_region);
            
            // Configurar el cliente S3 con la región correcta
            // El cliente automáticamente usará el endpoint correcto para la región
            var config = new AmazonS3Config
            {
                RegionEndpoint = regionEndpoint,
                ForcePathStyle = false, // Usar virtual-hosted-style (bucket.s3.region.amazonaws.com)
                UseHttp = false // Usar HTTPS
            };
            
            _s3Client = new AmazonS3Client(accessKey, secretKey, config);
        }

        /// <summary>
        /// Valida si un archivo es una imagen válida
        /// </summary>
        public bool IsValidImageFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return false;

            // Validar tamaño
            if (file.Length > _maxFileSize)
                return false;

            // Validar extensión
            var extension = Path.GetExtension(file.FileName)?.ToLowerInvariant();
            if (string.IsNullOrEmpty(extension) || !_allowedExtensions.Contains(extension))
                return false;

            // Validar Content-Type
            if (string.IsNullOrWhiteSpace(file.ContentType) || 
                !_allowedImageTypes.Contains(file.ContentType.ToLowerInvariant()))
                return false;

            return true;
        }

        /// <summary>
        /// Sube un archivo a S3 con validaciones de seguridad
        /// </summary>
        public async Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType)
        {
            try
            {
                // Validar Content-Type
                if (string.IsNullOrWhiteSpace(contentType) || 
                    !_allowedImageTypes.Contains(contentType.ToLowerInvariant()))
                {
                    throw new ArgumentException($"Tipo de archivo no permitido. Tipos permitidos: {string.Join(", ", _allowedImageTypes)}");
                }

                // Validar extensión del archivo
                var extension = Path.GetExtension(fileName)?.ToLowerInvariant();
                if (string.IsNullOrEmpty(extension) || !_allowedExtensions.Contains(extension))
                {
                    throw new ArgumentException($"Extensión de archivo no permitida. Extensiones permitidas: {string.Join(", ", _allowedExtensions)}");
                }

                // Generar nombre único y seguro para el archivo
                var sanitizedFileName = SanitizeFileName(fileName);
                var uniqueFileName = $"{Guid.NewGuid()}_{sanitizedFileName}";
                var key = $"blog/posts/{DateTime.UtcNow:yyyy/MM}/{uniqueFileName}";

                // Validar tamaño del stream
                if (fileStream.Length > _maxFileSize)
                {
                    throw new ArgumentException($"El archivo excede el tamaño máximo permitido ({_maxFileSize / (1024 * 1024)}MB)");
                }

                var request = new PutObjectRequest
                {
                    BucketName = _bucketName,
                    Key = key,
                    InputStream = fileStream,
                    ContentType = contentType,
                    // No usar CannedACL si el bucket tiene "Object Ownership" como "Bucket owner enforced"
                    // En su lugar, usar políticas de bucket para acceso público
                    ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256, // Encriptación en servidor
                    StorageClass = S3StorageClass.Standard // Clase de almacenamiento estándar
                };

                // Agregar metadata adicional
                request.Metadata.Add("uploaded-by", "blog-api");
                request.Metadata.Add("upload-date", DateTime.UtcNow.ToString("O"));
                request.Metadata.Add("original-filename", fileName);

                // Headers para cache y optimización
                request.Headers["Cache-Control"] = "public, max-age=31536000"; // Cache de 1 año

                var response = await _s3Client.PutObjectAsync(request);

                if (response.HttpStatusCode != System.Net.HttpStatusCode.OK)
                {
                    throw new Exception($"Error al subir el archivo a S3. Status: {response.HttpStatusCode}");
                }

                // Generar URL pública usando el endpoint correcto de la región
                // Para sa-east-1 el formato es: bucket.s3.region.amazonaws.com
                // Si la región es us-east-1, el formato es diferente (sin .region)
                string fileUrl;
                if (_region == "us-east-1")
                {
                    fileUrl = $"https://{_bucketName}.s3.amazonaws.com/{key}";
                }
                else
                {
                    fileUrl = $"https://{_bucketName}.s3.{_region}.amazonaws.com/{key}";
                }
                
                return fileUrl;
            }
            catch (AmazonS3Exception ex)
            {
                // Si el error es sobre el endpoint, es probable que la región del bucket sea diferente
                if (ex.Message.Contains("must be addressed using the specified endpoint") || 
                    ex.Message.Contains("specified endpoint"))
                {
                    throw new Exception(
                        $"Error de AWS S3: La región configurada ({_region}) no coincide con la región real del bucket '{_bucketName}'. " +
                        $"Por favor, verifica en la consola de AWS cuál es la región real del bucket y actualiza la configuración en appsettings.json. " +
                        $"Error original: {ex.Message}", ex);
                }
                throw new Exception($"Error de AWS S3: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al subir archivo: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Elimina un archivo de S3
        /// </summary>
        public async Task<bool> DeleteFileAsync(string fileUrl)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(fileUrl))
                    return false;

                // Extraer la key (ruta) del archivo desde la URL
                var key = ExtractKeyFromUrl(fileUrl);
                if (string.IsNullOrEmpty(key))
                {
                    return false;
                }

                var deleteRequest = new DeleteObjectRequest
                {
                    BucketName = _bucketName,
                    Key = key
                };

                var response = await _s3Client.DeleteObjectAsync(deleteRequest);

                return response.HttpStatusCode == System.Net.HttpStatusCode.NoContent ||
                       response.HttpStatusCode == System.Net.HttpStatusCode.OK;
            }
            catch (AmazonS3Exception ex)
            {
                // Log del error (en producción usar ILogger)
                Console.WriteLine($"Error al eliminar archivo de S3: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error inesperado al eliminar archivo: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Sanitiza el nombre del archivo para evitar caracteres especiales
        /// </summary>
        private string SanitizeFileName(string fileName)
        {
            // Remover caracteres peligrosos
            var invalidChars = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
            var invalidRegex = new Regex($"[{invalidChars}]");
            var sanitized = invalidRegex.Replace(fileName, "_");

            // Limitar longitud
            if (sanitized.Length > 100)
            {
                var extension = Path.GetExtension(sanitized);
                var nameWithoutExtension = Path.GetFileNameWithoutExtension(sanitized);
                sanitized = nameWithoutExtension.Substring(0, 100 - extension.Length) + extension;
            }

            return sanitized;
        }

        /// <summary>
        /// Extrae la key (ruta) del archivo desde la URL de S3
        /// </summary>
        private string? ExtractKeyFromUrl(string fileUrl)
        {
            try
            {
                // Formato esperado: https://bucket.s3.region.amazonaws.com/key
                // o: https://bucket.s3.amazonaws.com/key
                var uri = new Uri(fileUrl);
                var key = uri.PathAndQuery.TrimStart('/');

                // Verificar que la URL pertenece al bucket configurado
                if (!uri.Host.Contains(_bucketName))
                {
                    return null;
                }

                return key;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Valida si una URL de imagen es válida y pertenece al bucket configurado
        /// </summary>
        public bool IsValidImageUrl(string? imageUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
                return false;

            try
            {
                // Validar formato de URL
                if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri) || 
                    (uri.Scheme != "http" && uri.Scheme != "https"))
                {
                    return false;
                }

                // Validar que pertenece al bucket configurado
                if (!uri.Host.Contains(_bucketName))
                {
                    return false;
                }

                // Validar que es del dominio de S3 de AWS
                if (!uri.Host.Contains(".s3.") && !uri.Host.Contains("s3.amazonaws.com"))
                {
                    return false;
                }

                // Validar extensión de archivo
                var path = uri.AbsolutePath;
                var extension = Path.GetExtension(path)?.ToLowerInvariant();
                if (string.IsNullOrEmpty(extension) || !_allowedExtensions.Contains(extension))
                {
                    return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Verifica si una imagen existe en S3
        /// </summary>
        public async Task<bool> ImageExistsAsync(string? imageUrl)
        {
            if (!IsValidImageUrl(imageUrl))
                return false;

            try
            {
                var key = ExtractKeyFromUrl(imageUrl!);
                if (string.IsNullOrEmpty(key))
                    return false;

                var request = new GetObjectMetadataRequest
                {
                    BucketName = _bucketName,
                    Key = key
                };

                var response = await _s3Client.GetObjectMetadataAsync(request);
                
                // Verificar que el ContentType es una imagen
                if (string.IsNullOrWhiteSpace(response.ContentType) || 
                    !_allowedImageTypes.Contains(response.ContentType.ToLowerInvariant()))
                {
                    return false;
                }

                return true;
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}
