# Gregal
Gregal/
├── Data/                 # Acceso a Base de Datos (Firebird)
├── Models/               # Entidades (la clase Historico que creamos)
├── Networking/           # Lógica del servidor TCP (Sockets)
├── Parsing/              # Lógica para convertir texto a objetos
├── Services/             # El Worker Service principal
└── Utils/                # Formateadores, logs o constantes

Detalles de Namespaces y Responsabilidades
1. Gregal.Models

Aquí colocas la clase Historico.cs. Es el "corazón" que todos los demás componentes conocen.

    Namespace: Gregal.Models

2. Gregal.Data

Aquí creas una clase llamada HistoricoRepository.cs. Esta clase contendrá el FbConnection y el método Insertar(Historico registro).

    Namespace: Gregal.Data

    Objetivo: Aislar todo el SQL y las cadenas de conexión de Firebird.

3. Gregal.Networking

Aquí creas una clase llamada TcpServer.cs. Se encarga de abrir el puerto y recibir los bytes.

    Namespace: Gregal.Networking

    Objetivo: Manejar la conexión de red sin preocuparse de qué dicen los datos o dónde se guardan.

4. Gregal.Parsing

Crea una clase llamada GregalParser.cs. Esta clase recibe el string o byte[] que llega por TCP y devuelve un objeto Historico relleno.

    Namespace: Gregal.Parsing

    Objetivo: Centralizar las reglas de negocio (ej. cómo separar los campos, formatos de fecha, etc.).

5. Gregal.Services

Aquí estará el archivo Worker.cs que genera Visual Studio por defecto.

    Namespace: Gregal (o Gregal.Services)

    Objetivo: Es el "director de orquesta". Llama al TcpServer para obtener datos, al Parser para procesarlos y al Repository para guardarlos.