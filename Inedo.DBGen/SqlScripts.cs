namespace Inedo.Data.CodeGenerator;

internal static class SqlScripts
{
    public const string GetTablesQuery = """
        DECLARE @UninclusedTables TABLE ([Object_Name] SYSNAME NOT NULL) 

        IF OBJECT_ID('[__UninclusedObjects]') IS NOT NULL
            INSERT INTO @UninclusedTables
            SELECT [Object_Name]
            FROM [__UninclusedObjects]
        
        SELECT tab.name,
        	   c.name,
        	   c.is_nullable,
        	   CASE WHEN t.name = 'YNINDICATOR' OR c.name LIKE '%\_Indicator' ESCAPE '\' THEN 'YNINDICATOR'
        			WHEN t.name = 'YNINDICATOR' OR t2.name IS NULL THEN t.name
               ELSE t2.name END,
        	   c.max_length,
               c.precision,
               c.scale
          FROM sys.columns c
               INNER JOIN (SELECT name,
        	                      object_id
        					 FROM sys.tables innertables
        				   UNION ALL
        				   SELECT name,
        				          object_id
        					 FROM sys.views innerviews) tab
        	           ON tab.object_id = c.object_id
        	   INNER JOIN sys.types t
        	           ON c.user_type_id = t.user_type_id
        	    LEFT JOIN sys.types t2
        	           ON t2.system_type_id = t.system_type_id
        			  AND t2.user_type_id = t2.system_type_id
         WHERE LEFT(tab.name, 2) <> '__'
           AND tab.name NOT IN (SELECT [Object_Name] FROM @UninclusedTables)
         ORDER BY tab.name, c.name
        """;

    public const string GetStoredProcsQuery = """
        SELECT p.object_id,
               p.name,
        	   m.definition,
        	   SPI.[ReturnType_Name],
        	   SPI.[DataTableNames_Csv],
        	   SPI.[Description_Text],
        	   SPI.[Remarks_Text]
          FROM sys.procedures p
               INNER JOIN sys.sql_modules m
        	           ON p.object_id = m.object_id
        	   LEFT JOIN __StoredProcInfo SPI
        	          ON SPI.[StoredProc_Name] = p.name
         WHERE LEFT(name, 2) <> '__'
         ORDER BY name
        """;

		public const string GetStoredProcParamsQuery = """
        SELECT p.object_id,
               RIGHT(p.name, LEN(p.name) - 1),
               p.max_length,
        	   CASE WHEN t.name = 'YNINDICATOR' OR t2.name IS NULL THEN t.name ELSE t2.name END,
        	   t.is_table_type,
        	   p.is_output
          FROM sys.parameters p
        	   INNER JOIN sys.types t
        	           ON p.user_type_id = t.user_type_id
        	    LEFT JOIN sys.types t2
        	           ON t2.system_type_id = t.system_type_id
        			  AND t2.user_type_id = t2.system_type_id
         WHERE p.name <> ''
         ORDER BY p.object_id, p.parameter_id
        """;

    public const string GetUserDefinedTables = """
        SELECT type_table_object_id,
               name
          FROM sys.table_types
         WHERE is_user_defined = 1
           AND is_table_type = 1
         ORDER BY name
        """;

    public const string GetUserDefinedTableColumns = """
        SELECT c.object_id,
        	   c.name,
        	   CASE WHEN t.name = 'YNINDICATOR' OR t2.name IS NULL THEN t.name ELSE t2.name END,
        	   c.max_length,
        	   c.is_nullable,
               c.precision,
               c.scale
          FROM sys.columns c
               INNER JOIN sys.table_types tt
        	           ON c.object_id = tt.type_table_object_id
        	   INNER JOIN sys.types t
        	           ON c.user_type_id = t.user_type_id
        	    LEFT JOIN sys.types t2
        	           ON t2.system_type_id = t.system_type_id
        			  AND t2.user_type_id = t2.system_type_id
         WHERE tt.is_user_defined = 1
           AND tt.is_table_type = 1
         ORDER BY c.object_id, c.column_id
        """;
}
