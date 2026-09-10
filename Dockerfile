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

# La hora del servidor.
#
# Render corre sus máquinas con la hora de Londres (UTC), tres horas adelante de Jujuy.
# Sin esto, después de las 21:00 el sistema ya cree que es el día siguiente y fecha mal
# todo lo que se cargue de noche: gastos, aportes, retiros, reservas, avisos del inicio.
#
# Son dos cosas y van juntas: TZ le dice a .NET en qué zona está, y tzdata (que se
# instala acá abajo) es la tabla de zonas horarias que necesita para entenderlo. Sin
# tzdata, TZ se ignora en silencio y todo sigue en Londres.
#
# Argentina está en UTC-3 todo el año: no movemos el reloj desde 2009.
ENV TZ=America/Argentina/Jujuy
ENV DEBIAN_FRONTEND=noninteractive

# libfontconfig1: la necesita QuestPDF (los PDF del seguro) para dibujar texto en Linux
# tzdata: la tabla de zonas horarias, para que la variable TZ de arriba sirva de algo
RUN apt-get update \
    && apt-get install -y --no-install-recommends libfontconfig1 tzdata \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish ./

# Render nos dirá el puerto real con la variable PORT; esto es solo informativo
EXPOSE 8080

ENTRYPOINT ["dotnet", "Wamani.Reservas.dll"]
