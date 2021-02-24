namespace Inedo.Data.CodeGenerator
{
    internal static class SqlScripts
    {
        public const string GetTablesQuery = @"
SELECT TABLE_NAME, COLUMN_NAME, IS_NULLABLE, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH
  FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME NOT LIKE '!_!_%' ESCAPE '!'
 ORDER BY TABLE_NAME, COLUMN_NAME
";

        public const string GetStoredProcsQuery = @"
SELECT [StoredProc_Name] = R.ROUTINE_NAME
      ,SPI.[ReturnType_Name]
      ,SPI.[DataTableNames_Csv]
      ,SPI.[OutputPropertyNames_Csv]
      ,SPI.[Description_Text]
      ,SPI.[Remarks_Text]
      ,R.[Routine_Definition]
 FROM INFORMATION_SCHEMA.ROUTINES R
      LEFT JOIN [{0}__StoredProcInfo] SPI
             ON R.ROUTINE_NAME = SPI.[StoredProc_Name]
WHERE R.ROUTINE_NAME NOT IN('Events_RaiseEvent', 'HandleError')
  AND R.ROUTINE_TYPE = 'PROCEDURE'
  AND LEFT(R.ROUTINE_NAME, 2) <> '__'
ORDER BY R.ROUTINE_NAME";
    }
}
