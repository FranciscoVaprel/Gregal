-- Amplía HISTORICO.BOLAS de VARCHAR(255) a VARCHAR(300).
--
-- Motivo: una partida con casi todas las bolas extraídas (hasta 90, lista
-- separada por comas) puede ocupar hasta 260 caracteres. Con el protocolo de
-- eventos "tag" (TagSerialGregalReceiver/PartidaAccumulator) esto se alcanza
-- con cualquier partida larga, no es un caso raro — se detectó al probar una
-- partida real de ejemplo con 89 bolas (262 caracteres), que Firebird
-- rechazaba con "string right truncation".
--
-- Aplicar en CUALQUIER base de datos HISTORICO ya desplegada (no solo en
-- GREGAL_EMPTY.FDB de desarrollo/pruebas), antes de poner en marcha el
-- receptor del protocolo de eventos en una sala real.

ALTER TABLE HISTORICO ALTER COLUMN BOLAS TYPE VARCHAR(300);
