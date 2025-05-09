using InternationalSynchronizer.UpdateVectorItemTable.Model;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace InternationalSynchronizer.Utilities
{
    public static class SqlQuery
    {
        private static string LayerQuery(Layer layer) => layer switch
        {
            Layer.Subject => "sub",
            Layer.Package => "pac",
            Layer.Theme => "thm",
            Layer.Knowledge => "kno",
            Layer.KnowledgeType => "kno",
            _ => "",
        };

        private static string IsSyncItemDeletedQuery(Layer layer)
        {
            if (layer == Layer.Subject || layer == Layer.KnowledgeType)
                return "";

            string query = $"CASE WHEN sync_{LayerQuery(layer)}.date_deleted IS NULL ";

            if (layer == Layer.Package)
                query += $"THEN COALESCE(sync_pac.name  + ' - ' + sync_pac.description, '') ";
            else if (layer == Layer.Theme)
                query += $"THEN COALESCE(sync_thm.name, '') ";
            else
                query += $"THEN COALESCE(sync_kno.knowledge_text_preview, '') ";

            return query + $"ELSE 'POLOŽKA BOLA ODSTRÁNENÁ - ID: ' + CAST(sync_{LayerQuery(layer)}.id AS VARCHAR(10)) END ";
        }

        private static string SyncedChildrenRatioQuery()
        {
            return @"CAST(
                         COUNT(CASE 
                             WHEN pair_children.id_1 IS NOT NULL 
                             THEN pair_children.id_1
                             END
                         ) AS VARCHAR(10)
                     ) + '/' + CAST( COUNT(*) AS VARCHAR(10) )";
        }

        private static string PairQuery(bool child = false)
        {
            return $@"
                SELECT sync_item_1.id_item AS id_1, sync_item_2.id_item AS id_2
                FROM sync_pair AS sync_pair
                JOIN sync_item AS sync_item_1
                    ON (sync_pair.id_sync_item_1 = sync_item_1.id
                    OR sync_pair.id_sync_item_2 = sync_item_1.id)
                    AND sync_item_1.id_database = @dbSource AND sync_item_1.id_item_type = @itemType {(child ? " + 1" : "")}
                JOIN sync_item AS sync_item_2
                    ON sync_item_2.id_database = @dbDest
                    AND sync_item_2.id = CASE 
                                            WHEN sync_pair.id_sync_item_1 = sync_item_1.id 
                                            THEN sync_pair.id_sync_item_2 
                                            ELSE sync_pair.id_sync_item_1 
                                        END";
        }

        public static string DataQuery(Layer layer, Int32 id, Int32 sourceDatabaseId, Int32 targetDatabaseId)
        {
            return layer switch
            {
                Layer.Subject => Subjects(sourceDatabaseId, targetDatabaseId),
                Layer.Package => Packages(sourceDatabaseId, targetDatabaseId, id),
                Layer.Theme => Themes(sourceDatabaseId, targetDatabaseId, id),
                Layer.Knowledge => Knowledge(sourceDatabaseId, targetDatabaseId, id),
                Layer.KnowledgeType => SpecificKnowledge(sourceDatabaseId, targetDatabaseId, id),
                _ => "",
            };
        }

        private static string Subjects(Int32 db1, Int32 db2)
        {
            return $@"{DECLAREQuery(db1, db2, 0, 0)}

                SELECT
                    sub.id,
                    sub.name                                            AS Subject,
                    -1                                                  AS KnowledgeTypeId,
                    {SyncedChildrenRatioQuery()}                        AS SyncedChildrenRatio,
                    COALESCE(sync_sub.name, '')						    AS PairedItemSubject,
                    COALESCE(sync_sub.id, -1)						    AS PairedItemId



                FROM (SELECT * FROM subject_type_union AS sub WHERE sub.db_source = @dbSource) AS sub

                LEFT JOIN ({PairQuery()}) AS pair
                    ON pair.id_1 = sub.id
                LEFT JOIN subject_type_union AS sync_sub
                    ON pair.id_2 = sync_sub.id AND sync_sub.db_source = @dbDest

                LEFT JOIN package_union AS pac
                    ON pac.db_source = @dbSource
                    AND pac.id_subject_type = sub.id
                    AND pac.date_deleted IS NULL
                    AND pac.name NOT LIKE '[*]IMPORT[*]%'

                LEFT JOIN ({PairQuery(true)}) AS pair_children
                    ON pair_children.id_1 = pac.id

                GROUP BY
                    sub.id, sub.name, sync_sub.name, sync_sub.id

                ORDER BY sub.id;";
        }

        private static string Packages(Int32 db1, Int32 db2, Int32 id)
        {
            return $@"{DECLAREQuery(db1, db2, 1, id)}

                SELECT
                    pac.id,
                    sub.name                                                            AS Subject,
                    -1                                                                  AS KnowledgeTypeId,
                    COALESCE(pac.name + ' - ' + pac.description, '')					AS Package,
                    {SyncedChildrenRatioQuery()}                                        AS SyncedChildrenRatio,
                    COALESCE(sync_sub.name, '')						                    AS PairedItemSubject,
                    {IsSyncItemDeletedQuery(Layer.Package)}								AS PairedItemPackage,
                    COALESCE(sync_pac.id, -1)							                AS PairedItemId



                FROM subject_type_union AS sub
                INNER JOIN package_union AS pac
                    ON sub.id = pac.id_subject_type
                    AND sub.db_source LIKE pac.db_source
                    AND pac.date_deleted IS NULL
                    AND pac.name NOT LIKE '[*]IMPORT[*]%'
                    AND sub.db_source = @dbSource
                    AND sub.id = @itemId

                LEFT JOIN ({PairQuery()}) AS pair
                    ON pair.id_1 = pac.id
                LEFT JOIN package_union AS sync_pac
                    ON pair.id_2 = sync_pac.id
                    AND sync_pac.db_source = @dbDest
                LEFT JOIN subject_type_union AS sync_sub
                    ON sync_sub.id = sync_pac.id_subject_type
                    AND sync_sub.db_source = @dbDest

                LEFT JOIN theme_union AS thm
                    ON thm.db_source = @dbSource
                    AND thm.id_package = pac.id
                    AND thm.date_deleted IS NULL
                    AND thm.name NOT LIKE '[*]IMPORT[*]%'

                LEFT JOIN ({PairQuery(true)}) AS pair_children
                    ON pair_children.id_1 = thm.id

                GROUP BY
                    sub.id, sub.name, sync_sub.name, sync_sub.id, pac.name + ' - ' + pac.description, pac.id,
                    sync_pac.date_deleted, sync_pac.name  + ' - ' + sync_pac.description, sync_pac.id

                ORDER BY pac.id;";
        }

        private static string Themes(Int32 db1, Int32 db2, Int32 id)
        {
            return $@"{DECLAREQuery(db1, db2, 2, id)}

                SELECT
                    thm.id,
                    sub.name                                                    AS Subject,
                    COALESCE(pac.name + ' - ' + pac.description, '')			AS Package,
                    -1                                                          AS KnowledgeTypeId,
                    COALESCE(thm.name,'')										AS Theme,
                    {SyncedChildrenRatioQuery()}                                AS SyncedChildrenRatio,
                    COALESCE(sync_sub.name, '')						            AS PairedItemSubject,
                    COALESCE(sync_pac.name  + ' - ' + sync_pac.description, '')	AS PairedItemPackage,
                    {IsSyncItemDeletedQuery(Layer.Theme)}						AS PairedItemTheme,
                    COALESCE(sync_thm.id, -1)							        AS PairedItemId



                FROM subject_type_union AS sub
                INNER JOIN package_union AS pac
                    ON sub.id = pac.id_subject_type
                    AND pac.db_source = @dbSource
                    AND pac.date_deleted IS NULL
                    AND pac.name NOT LIKE '[*]IMPORT[*]%'
                    AND sub.db_source = @dbSource
                    AND pac.id = @itemId
                INNER JOIN theme_union AS thm
                    ON pac.id = thm.id_package
                    AND thm.db_source = @dbSource
                    AND thm.date_deleted IS NULL
                    AND thm.name NOT LIKE '[*]IMPORT[*]%'

                LEFT JOIN ({PairQuery()}) AS pair
                    ON pair.id_1 = thm.id
                LEFT JOIN theme_union AS sync_thm
                    ON pair.id_2 = sync_thm.id AND sync_thm.db_source = @dbDest
                LEFT JOIN package_union AS sync_pac
                    ON sync_pac.id = sync_thm.id_package AND sync_pac.db_source = @dbDest
                LEFT JOIN subject_type_union AS sync_sub
                    ON sync_sub.id = sync_pac.id_subject_type AND sync_sub.db_source = @dbDest

                LEFT JOIN theme_part_union AS thm_p
                    ON thm_p.db_source = @dbSource AND thm_p.id_theme = thm.id
                LEFT JOIN knowledge_union AS kno
                    ON kno.id_theme_part = thm_p.id
                    AND kno.db_source = @dbSource
                    AND kno.date_deleted IS NULL
                    AND kno.knowledge_text_preview NOT LIKE '[*]IMPORT[*]%'

                LEFT JOIN ({PairQuery(true)}) AS pair_children
                    ON pair_children.id_1 = kno.id

                GROUP BY
                    sub.id, sub.name, sync_sub.name, pac.name + ' - ' + pac.description, sync_thm.id,
                    sync_pac.name  + ' - ' + sync_pac.description, thm.name, thm.id, sync_thm.date_deleted,
                    sync_thm.name, sync_pac.id

                ORDER BY thm.id;";
        }

        private static string Knowledge(Int32 db1, Int32 db2, Int32 id)
        {
            return $@"{DECLAREQuery(db1, db2, 3, id)}

                SELECT
                     kno.id,
                     sub.name                                                       AS Subject,
                     COALESCE(pac.name + ' - ' + pac.description, '')				AS Package,
                     COALESCE(thm.name,'')											AS Theme,
                     COALESCE(thm_p.name, '')										AS ThemePart,
                     COALESCE(kno.knowledge_text_preview, '')						AS Knowledge,
                     COALESCE(kno_t.name, '')							            AS KnowledgeType,
                     kno_t.id											            AS KnowledgeTypeId,
                     COALESCE(sync_sub.name, '')						            AS PairedItemSubject,
                     COALESCE(sync_pac.name  + ' - ' + sync_pac.description, '')	AS PairedItemPackage,
                     COALESCE(sync_thm.name, '')						            AS PairedItemTheme,
                     COALESCE(sync_thm_p.name, '')						            AS PairedItemThemePart,
                     {IsSyncItemDeletedQuery(Layer.Knowledge)}						AS PairedItemKnowledge,
                     COALESCE(sync_kno_t.name, '')						            AS PairedItemKnowledgeType,
                     COALESCE(sync_kno.id, -1)							            AS PairedItemId



                FROM subject_type_union AS sub
                INNER JOIN package_union AS pac
                    ON sub.id = pac.id_subject_type
                    AND pac.db_source = @dbSource
                    AND pac.date_deleted IS NULL
                    AND pac.name NOT LIKE '[*]IMPORT[*]%'
                    AND sub.db_source = @dbSource
                INNER JOIN theme_union AS thm
                    ON pac.id = thm.id_package
                    AND thm.db_source = @dbSource
                    AND thm.date_deleted IS NULL
                    AND thm.name NOT LIKE '[*]IMPORT[*]%'
                    AND thm.id = @itemId
                INNER JOIN theme_part_union AS thm_p
                    ON thm_p.id_theme = thm.id AND thm_p.db_source = @dbSource
                INNER JOIN knowledge_union AS kno
                    ON kno.id_theme_part = thm_p.id
                    AND kno.db_source = @dbSource
                    AND kno.date_deleted IS NULL
                    AND kno.knowledge_text_preview NOT LIKE '[*]IMPORT[*]%'
                INNER JOIN knowledge_type_union AS kno_t
                    ON kno_t.id = kno.id_knowledge_type AND kno_t.db_source = @dbSource

                LEFT JOIN ({PairQuery()}) AS pair
                    ON pair.id_1 = kno.id
                LEFT JOIN knowledge_union AS sync_kno
                    ON pair.id_2 = sync_kno.id AND sync_kno.db_source = @dbDest
                LEFT JOIN theme_part_union AS sync_thm_p
                    ON sync_thm_p.id = sync_kno.id_theme_part AND sync_thm_p.db_source = @dbDest
                LEFT JOIN theme_union AS sync_thm
                    ON sync_thm.id = sync_thm_p.id_theme AND sync_thm.db_source = @dbDest
                LEFT JOIN package_union AS sync_pac
                    ON sync_pac.id = sync_thm.id_package AND sync_pac.db_source = @dbDest
                LEFT JOIN subject_type_union AS sync_sub
                    ON sync_sub.id = sync_pac.id_subject_type AND sync_sub.db_source = @dbDest
                LEFT JOIN knowledge_type_union AS sync_kno_t
                    ON sync_kno.id_knowledge_type = sync_kno_t.id AND sync_kno_t.db_source = @dbDest

                ORDER BY kno.id;";
        }

        private static string SpecificKnowledge(Int32 db1, Int32 db2, Int32 id)
        {
            return $@"{DECLAREQuery(db1, db2, 3, id)}

                SELECT
                     kno.id,
                     sub.name                                                       AS Subject,
                     COALESCE(pac.name + ' - ' + pac.description, '')				AS Package,
                     COALESCE(thm.name,'')										    AS Theme,
                     COALESCE(thm_p.name, '')										AS ThemePart,
                     COALESCE(kno.knowledge_text_preview, '')						AS Knowledge,
                     COALESCE(kno_t.name, '')							            AS KnowledgeType,
                     kno_t.id											            AS KnowledgeTypeId,
                     COALESCE(sync_sub.name, '')						            AS PairedItemSubject,
                     COALESCE(sync_pac.name  + ' - ' + sync_pac.description, '')	AS PairedItemPackage,
                     COALESCE(sync_thm.name, '')						            AS PairedItemTheme,
                     COALESCE(sync_thm_p.name, '')						            AS PairedItemThemePart,
                     {IsSyncItemDeletedQuery(Layer.Knowledge)}						AS PairedItemKnowledge,
                     COALESCE(sync_kno_t.name, '')						            AS PairedItemKnowledgeType,
                     COALESCE(sync_kno.id, -1)							            AS PairedItemId



                FROM subject_type_union AS sub
                INNER JOIN package_union AS pac
                    ON sub.id = pac.id_subject_type
                    AND pac.db_source = @dbSource
                    AND pac.date_deleted IS NULL
                    AND pac.name NOT LIKE '[*]IMPORT[*]%'
                    AND sub.db_source = @dbSource
                INNER JOIN theme_union AS thm
                    ON pac.id = thm.id_package
                    AND thm.db_source = @dbSource
                    AND thm.date_deleted IS NULL
                    AND thm.name NOT LIKE '[*]IMPORT[*]%'
                INNER JOIN theme_part_union AS thm_p
                    ON thm_p.id_theme = thm.id AND thm_p.db_source = @dbSource
                INNER JOIN knowledge_union AS kno
                    ON kno.id_theme_part = thm_p.id
                    AND kno.db_source = @dbSource
                    AND kno.date_deleted IS NULL
                    AND kno.knowledge_text_preview NOT LIKE '[*]IMPORT[*]%'
                    AND kno.id = @itemId
                INNER JOIN knowledge_type_union AS kno_t
                    ON kno_t.id = kno.id_knowledge_type AND kno_t.db_source = @dbSource

                LEFT JOIN ({PairQuery()}) AS pair
                    ON pair.id_1 = kno.id
                LEFT JOIN knowledge_union AS sync_kno
                    ON pair.id_2 = sync_kno.id AND sync_kno.db_source = @dbDest
                LEFT JOIN theme_part_union AS sync_thm_p
                    ON sync_thm_p.id = sync_kno.id_theme_part AND sync_thm_p.db_source = @dbDest
                LEFT JOIN theme_union AS sync_thm
                    ON sync_thm.id = sync_thm_p.id_theme AND sync_thm.db_source = @dbDest
                LEFT JOIN package_union AS sync_pac
                    ON sync_pac.id = sync_thm.id_package AND sync_pac.db_source = @dbDest
                LEFT JOIN subject_type_union AS sync_sub
                    ON sync_sub.id = sync_pac.id_subject_type AND sync_sub.db_source = @dbDest
                    LEFT JOIN knowledge_type_union AS sync_kno_t
                ON sync_kno.id_knowledge_type = sync_kno_t.id AND sync_kno_t.db_source = @dbDest

                ORDER BY kno.id;";
        }

        private static string DECLAREQuery(Int32 mainDatabaseId, Int32 secondaryDatabaseId, int layer, Int32 id)
        {
            return $@"  DECLARE 
                            @dbSource       CHAR(1) = '{mainDatabaseId}',
                            @dbDest         CHAR(1) = '{secondaryDatabaseId}',
                            @itemType		INT     = {layer},
                            @itemId		    INT     = {id}
                    ";
        }

        private static string SELECTQuery(Layer layer)
        {
            string query = $"SELECT sub.name AS sub_name ";
            if (layer == Layer.Subject)
                return query;

            query += $", pac.name + ' - ' + pac.description AS pac_name ";
            if (layer == Layer.Package)
                return query;
            query += ", thm.name AS thm_name ";
            if (layer == Layer.Theme)
                return query;
            return query + @",thm_p.name                  AS thm_p_name,
                              kno.knowledge_text_preview  AS kno_name,
                              kno_t.name                  AS kno_t_name ";
        }

        private static string FROMQuery(Layer layer, string database)
        {
            string query = $"FROM subject_type_union AS sub ";

            if (layer == Layer.Subject)
                return query + "WHERE sub.id = @itemId";

            query += $"JOIN package_union AS pac ON sub.id = pac.id_subject_type AND pac.db_source = {database} AND sub.db_source = {database} ";

            if (layer == Layer.Package)
                return query + "WHERE pac.id = @itemId";

            query += $"JOIN theme_union AS thm ON pac.id = thm.id_package AND thm.db_source = {database} ";

            if (layer == Layer.Theme)
                return query + "WHERE thm.id = @itemId";

            return query + $@"
                            JOIN theme_part_union AS thm_p
                            ON thm.id = thm_p.id_theme AND thm_p.db_source = {database}
                            JOIN knowledge_union AS kno
                            ON thm_p.id = kno.id_theme_part AND kno.db_source = {database}
                            WHERE kno.id = @itemId";
        }

        public static string OneItemQuery(Layer layer, Int32 id, Int32 databaseId)
        {
            return DECLAREQuery(0, databaseId, (int) layer, id) + SELECTQuery(layer) + FROMQuery(layer, "@dbDest");
        }

        public static string DatabaseIdQuery(string database)
        {
            return $"SELECT id FROM sb_database WHERE name = '{database}'";
        }

        public static string InsertDatabaseQuery(string database)
        {
            return $"INSERT INTO sb_database (name) VALUES ('{database}')";
        }

        public static string SyncPairQuery(Int32 layer, Int32 id, Int32 itemDatabaseId, Int32 pairItemDatabaseId)
        {
            return @$"
            SELECT sync_item_1.id_item, sync_item_2.id_item
            FROM sync_pair AS pair
            INNER JOIN sync_item AS sync_item_1 ON pair.id_sync_item_1 = sync_item_1.id
            INNER JOIN sync_item AS sync_item_2 ON pair.id_sync_item_2 = sync_item_2.id
            WHERE
                (
                    sync_item_1.id_item = {id}
                    AND sync_item_1.id_database = {itemDatabaseId}
                    AND sync_item_1.id_item_type = {layer}
                    AND sync_item_2.id_database = {pairItemDatabaseId}
                )
                OR
                (
                    sync_item_2.id_item = {id}
                    AND sync_item_2.id_database = {itemDatabaseId}
                    AND sync_item_2.id_item_type = {layer}
                    AND sync_item_1.id_database = {pairItemDatabaseId}
                )";
        }

        public static string SyncItemQuery(Int32 layer, Int32 id, Int32 databaseId)
        {
            return @$"
            SELECT id
            FROM sync_item
            WHERE
                id_database = {databaseId}
                AND id_item_type = {layer}
                AND id_item = {id}";
        }

        public static string InsertSyncItemQuery(Int32 layer, Int32 id, Int32 databaseId)
        {
            return $"INSERT INTO sync_item (id_item_type, id_item, id_database) VALUES ({layer}, {id}, {databaseId})";
        }

        public static string InsertSyncPairQuery(Int32 id1, Int32 id2)
        {
            return $"INSERT INTO sync_pair (id_sync_item_1, id_sync_item_2) VALUES ({id1}, {id2})";
        }

        public static string DeleteSyncPairQuery(Int32 id1, Int32 id2)
        {
            return @$"
            DELETE FROM sync_pair
            WHERE
                (id_sync_item_1 = {id1} AND id_sync_item_2 = {id2})
                OR
                (id_sync_item_1 = {id2} AND id_sync_item_2 = {id1})";
        }
    }
}
