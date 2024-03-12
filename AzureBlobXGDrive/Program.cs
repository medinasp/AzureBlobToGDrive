using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/upload-to-drive/{year}/{month}", async (int year, int month) =>
{
    var yearMonth = new DateTime(year, month, 1);
    string blobStorageConnectionString = "DefaultEndpointsProtocol=https;AccountName=nomedasuacontaAzure;AccountKey=chavedacontaAzure;EndpointSuffix=core.windows.net";
    string blobStorageContainerName = "filemanager";
    //abaixo o endereço onde vc quer fazer a "baldeacao" dos arquivos
    string localFolderPath = @"C:\1A\RepHist\ConAzureBlobStorageXGoogleDrive\AzureBlobXGDrive\bin\Debug\net7.0\";
    string googleDriveDirectoryId = ""; // ID do diretório de destino no Google Drive

    // Método para obter o serviço do Google Drive
    DriveService GetDriveService()
    {
        string[] scopes = { DriveService.Scope.Drive };
        string applicationName = "BStorageManager";

        GoogleCredential credential;
        //endereco para o json com suas credencias do google
        using (var stream = new FileStream(@"C:\1A\RepHist\ConAzureBlobStorageXGoogleDrive\credenciais.json", FileMode.Open, FileAccess.Read))
        {
            credential = GoogleCredential.FromStream(stream)
                .CreateScoped(scopes);
        }

        var service = new DriveService(new BaseClientService.Initializer()
        {
            HttpClientInitializer = credential,
            ApplicationName = applicationName,
        });

        return service;
    }

    // Criar um BlobServiceClient para interagir com o Blob Storage
    BlobServiceClient blobServiceClient = new BlobServiceClient(blobStorageConnectionString);

    // Obter uma referência ao contêiner de armazenamento
    BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(blobStorageContainerName);

    // Verificar se o contêiner existe
    if (!await containerClient.ExistsAsync())
    {
        Console.WriteLine($"O contêiner '{blobStorageContainerName}' não existe.");
        return Results.NotFound($"O contêiner '{blobStorageContainerName}' não existe.");
    }

    var driveService = GetDriveService(); // Método para obter o serviço do Google Drive

    await foreach (BlobItem blobItem in containerClient.GetBlobsAsync())
    {
        if (blobItem.Properties.LastModified.HasValue)
        {
            // Obter a data do blob
            var blobDate = blobItem.Properties.LastModified.Value.Date;

            // Verificar se a data do blob corresponde ao ano/mês especificado
            if (blobDate.Year == yearMonth.Year && blobDate.Month == yearMonth.Month)
            {
                // Obter uma referência ao blob
                BlobClient blobClient = containerClient.GetBlobClient(blobItem.Name);

                // Fazer o download do blob para um arquivo local
                string localFilePath = Path.Combine(localFolderPath, blobItem.Name);
                await blobClient.DownloadToAsync(localFilePath);

                Console.WriteLine($"O arquivo '{blobItem.Name}' foi baixado para '{localFilePath}' com sucesso.");

                // Fazer o upload do arquivo para o Google Drive
                using (var stream = new FileStream(localFilePath, FileMode.Open))
                {
                    var fileMetadata = new Google.Apis.Drive.v3.Data.File()
                    {
                        Name = blobItem.Name,
                        Parents = new[] { googleDriveDirectoryId }
                    };

                    var request = driveService.Files.Create(fileMetadata, stream, "application/octet-stream");
                    request.Upload();
                }

                Console.WriteLine($"O arquivo '{blobItem.Name}' foi enviado com sucesso para o Google Drive.");
            }
        }
    }

    Console.WriteLine("Todos os arquivos foram baixados e enviados com sucesso para o Google Drive.");

    return Results.Ok("Todos os arquivos foram baixados e enviados com sucesso para o Google Drive.");
})
.WithName("UploadBlobFilesToDrive")
.WithOpenApi();

app.Run();