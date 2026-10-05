namespace Gregal.Models;

// Un evento individual del protocolo *CODIGO<valor># (p.ej. *E19# -> ('E', "19")).
public readonly record struct TagEvent(char Code, string Value);
