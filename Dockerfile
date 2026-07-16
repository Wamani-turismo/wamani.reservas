# ---------------------------------------------------------------------------
# Dockerfile: la "receta" que usa Render para armar y correr el sistema.
# No tenés que ejecutar nada de esto a mano: Render lo hace solo al subir.
# ---------------------------------------------------------------------------

# Etapa 1: COMPILAR el sistema (usa el SDK de .NET, que es pesado)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiamos primero solo el archivo del proyecto para aprovechar la caché
COPY Wamani.Reservas.csproj ./
RUN dotnet restore Wamani.Reservas.csproj

# Ahora copiamos el resto del código y publicamos la versión final
COPY . ./
RUN dotnet publish Wamani.Reservas.csproj -c Release -o /app/publish /p:UseAppHost=false

# Etapa 2: CORRER el sistema (imagen liviana, solo lo necesario para funcionar)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Librería que necesita QuestPDF (los PDF del seguro) para dibujar texto en Linux
RUN apt-get update \
    && apt-get install -y --no-install-recommends libfontconfig1 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish ./

# Render nos dirá el puerto real con la variable PORT; esto es solo informativo
EXPOSE 8080

ENTRYPOINT ["dotnet", "Wamani.Reservas.dll"]
