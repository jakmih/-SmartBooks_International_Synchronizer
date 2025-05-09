namespace InternationalSynchronizer.UpdateVectorItemTable.DB
{
    class SqlQuery
    {
        public static string DatabaseIdQuery(string database)
        {
            return $"SELECT id FROM sb_database WHERE name = '{database}'";
        }

        public static string InsertDatabaseQuery(string database)
        {
            return $"INSERT INTO sb_database (name) VALUES ('{database}')";
        }

        public static string ClearVectorItemTable()
        {
            return "TRUNCATE TABLE vector_item";
        }

        public static string CreateTemporaryTable()
        {
            return @"
                CREATE TABLE #vector_item_staging (
                id_database INT, id_item INT, id_item_type INT, name NVARCHAR(MAX), 
                id_subject INT, id_package INT, id_theme INT, id_knowledge_type INT, date_modified DATETIME2)";
        }

        public static string DeleteRemovedRows()
        {
            return @"
                DELETE FROM vector_item
                WHERE NOT EXISTS (
                    SELECT 1 FROM #vector_item_staging AS vs
                    WHERE vs.id_item = vector_item.id_item
                        AND vs.id_item_type = vector_item.id_item_type
                        AND vs.id_database = vector_item.id_database)";
        }

        public static string UpdateChangedRows()
        {
            return @"
                UPDATE vi
                SET
                    vi.name = vs.name,
                    vi.id_subject = vs.id_subject,
                    vi.id_package = vs.id_package,
                    vi.id_theme = vs.id_theme,
                    vi.id_knowledge_type = vs.id_knowledge_type,
                    vi.date_modified = vs.date_modified
                FROM vector_item AS vi
                JOIN #vector_item_staging AS vs
                    ON vi.id_item = vs.id_item
                    AND vi.id_item_type = vs.id_item_type
                    AND vi.id_database = vs.id_database
                WHERE
                    ISNULL(vi.name, '') != ISNULL(vs.name, '')
                    OR vi.id_subject != vs.id_subject
                    OR ISNULL(vi.id_package, -1) != ISNULL(vs.id_package, -1)
                    OR ISNULL(vi.id_theme, -1) != ISNULL(vs.id_theme, -1)
                    OR ISNULL(vi.id_knowledge_type, -1) != ISNULL(vs.id_knowledge_type, -1)";
        }

        public static string InsertNewRows()
        {
            return @"
                INSERT INTO vector_item (id_database, id_item, id_item_type, name, id_subject, id_package, id_theme, id_knowledge_type, date_modified)
                SELECT vs.id_database, vs.id_item, vs.id_item_type, vs.name, vs.id_subject, vs.id_package, vs.id_theme, vs.id_knowledge_type, vs.date_modified
                FROM #vector_item_staging AS vs
                WHERE NOT EXISTS (
                    SELECT 1 FROM vector_item AS vi
                    WHERE vi.id_item = vs.id_item
                        AND vi.id_item_type = vs.id_item_type
                        AND vi.id_database = vs.id_database)";
        }

        public static string AllKnowledgesQuery()
        {
            return @"
            SELECT 
                sub.id, sub.name,
                pac.id, pac.name + ' - ' + pac.description,
                thm.id, thm.name,
                kno.id, kno.knowledge_text_preview, kno_t.id,
                sub.db_source
            FROM subject_type_union AS sub
            LEFT JOIN package_union AS pac
            ON sub.id = pac.id_subject_type
                AND sub.db_source = pac.db_source
                AND sub.name NOT LIKE '[*]IMPORT[*]%'
                AND pac.name NOT LIKE '[*]IMPORT[*]%'
                AND pac.date_deleted IS NULL
            LEFT JOIN theme_union AS thm
            ON pac.id = thm.id_package
                AND pac.db_source = thm.db_source
                AND thm.name NOT LIKE '[*]IMPORT[*]%'
                AND thm.date_deleted IS NULL
            LEFT JOIN theme_part_union AS thm_p
            ON thm_p.id_theme = thm.id
                AND thm.db_source = thm_p.db_source 
            LEFT JOIN knowledge_union AS kno
            ON kno.id_theme_part = thm_p.id
                AND thm_p.db_source = kno.db_source
                AND kno.knowledge_text_preview NOT LIKE '[*]IMPORT[*]%'
                AND kno.knowledge_text_preview IS NOT NULL
                AND kno.date_deleted IS NULL
            LEFT JOIN knowledge_type_union AS kno_t
            ON kno_t.id = kno.id_knowledge_type
                AND kno.db_source = kno_t.db_source
            ORDER BY sub.db_source, sub.id, pac.id, thm.id, kno.id";
        }
    }
}
