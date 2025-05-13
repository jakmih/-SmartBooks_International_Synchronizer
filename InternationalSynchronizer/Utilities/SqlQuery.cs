namespace InternationalSynchronizer.Utilities
{
    public static class SqlQuery
    {
        private static string IsSyncItemDeletedQuery(Layer layer) => layer switch
        {
            Layer.Package => @"CASE WHEN sync_pac.date_deleted IS NULL
                                    THEN COALESCE(sync_pac.name  + ' - ' + sync_pac.description, '')
                                    ELSE 'POLOŽKA BOLA ODSTRÁNENÁ - ID: ' + CAST(sync_pac.id AS VARCHAR(10))    END",

            Layer.Theme => @"CASE   WHEN sync_thm.date_deleted IS NULL
                                    THEN COALESCE(sync_thm.name, '')
                                    ELSE 'POLOŽKA BOLA ODSTRÁNENÁ - ID: ' + CAST(sync_thm.id AS VARCHAR(10))    END",

            Layer.Knowledge => @"CASE   WHEN sync_kno.date_deleted IS NULL
                                        THEN COALESCE(sync_kno.knowledge_text_preview, '')
                                        ELSE 'POLOŽKA BOLA ODSTRÁNENÁ - ID: ' + CAST(sync_kno.id AS VARCHAR(10))    END",

            Layer.KnowledgeType => @"CASE   WHEN sync.date_deleted IS NULL
                                            THEN COALESCE(sync.kno, '')
                                            ELSE 'POLOŽKA BOLA ODSTRÁNENÁ - ID: ' + CAST(sync.kno_id AS VARCHAR(10))    END",
            _ => "",
        };

        private static string SyncedChildrenRatioQuery(Layer layer)
        {
            return $@"   CAST(
                            COUNT( CASE 
                                WHEN sync_child.id_1 IS NOT NULL 
                                THEN 1
                                END
                            ) AS VARCHAR(10)
                        )
                        + '/' +
                        CAST(
                            COUNT( CASE 
                                WHEN child.id IS NOT NULL
                                THEN 1
                                END
                            ) AS VARCHAR(10)
                        ) ";
        }

        private static string PairQuery(bool child = false)
        {
            return $@"
                SELECT sync_item_1.id_item AS id_1, sync_item_2.id_item AS id_2
                FROM sync_pair AS sync_pair
                INNER JOIN sync_item AS sync_item_1
                    ON (sync_pair.id_sync_item_1 = sync_item_1.id
                    OR sync_pair.id_sync_item_2 = sync_item_1.id)
                    AND sync_item_1.id_database = @dbSource AND sync_item_1.id_item_type = @itemType {(child ? " + 1" : "")}
                INNER JOIN sync_item AS sync_item_2
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
            Layer layer = Layer.Subject;
            string source = App.Config["Tables:" + db1]!;
            string target = App.Config["Tables:" + db2]!;
            return $@"{DeclareQuery(db1, db2, layer, 0)}

                SELECT
                    sub.id,
                    sub.name                                            AS Subject,
                    -1                                                  AS KnowledgeTypeId,
                    {SyncedChildrenRatioQuery(layer)}                   AS SyncedChildrenRatio,
                    COALESCE(sync_sub.name, '')						    AS PairedItemSubject,
                    COALESCE(sync_sub.id, -1)						    AS PairedItemId



                FROM subject_type_{source} AS sub

                LEFT JOIN ({PairQuery()}) AS pair
                    ON pair.id_1 = sub.id
                LEFT JOIN subject_type_{target} AS sync_sub
                    ON pair.id_2 = sync_sub.id

                LEFT JOIN package_{source} AS child
                    ON child.id_subject_type = sub.id
                    AND child.date_deleted IS NULL
                    AND child.name NOT LIKE '[*]IMPORT[*]%'

                LEFT JOIN ({PairQuery(true)}) AS sync_child
                    ON sync_child.id_1 = child.id

                GROUP BY
                    sub.id, sub.name, sync_sub.name, sync_sub.id, sub.[order]

                ORDER BY sub.[order]";
        }

        private static string Packages(Int32 db1, Int32 db2, Int32 id)
        {
            Layer layer = Layer.Package;
            string source = App.Config["Tables:" + db1]!;
            string target = App.Config["Tables:" + db2]!;
            return $@"{DeclareQuery(db1, db2, layer, id)}

                SELECT
                    pac.id,
                    sub.name                                                            AS Subject,
                    -1                                                                  AS KnowledgeTypeId,
                    COALESCE(pac.name + ' - ' + pac.description, '')					AS Package,
                    {SyncedChildrenRatioQuery(layer)}                                   AS SyncedChildrenRatio,
                    COALESCE(sync_sub.name, '')						                    AS PairedItemSubject,
                    {IsSyncItemDeletedQuery(layer)}								        AS PairedItemPackage,
                    COALESCE(sync_pac.id, -1)							                AS PairedItemId



                FROM subject_type_{source} AS sub
                INNER JOIN package_{source} AS pac
                    ON sub.id = pac.id_subject_type
                    AND pac.date_deleted IS NULL
                    AND pac.name NOT LIKE '[*]IMPORT[*]%'
                    AND sub.id = @itemId

                LEFT JOIN ({PairQuery()}) AS pair
                    ON pair.id_1 = pac.id
                LEFT JOIN package_{target} AS sync_pac
                    ON pair.id_2 = sync_pac.id
                LEFT JOIN subject_type_{target} AS sync_sub
                    ON sync_sub.id = sync_pac.id_subject_type

                LEFT JOIN theme_{source} AS child
                    ON child.id_package = pac.id
                    AND child.date_deleted IS NULL
                    AND child.name NOT LIKE '[*]IMPORT[*]%'

                LEFT JOIN ({PairQuery(true)}) AS sync_child
                    ON sync_child.id_1 = child.id

                GROUP BY
                    sub.id, sub.name, sync_sub.name, sync_sub.id, pac.name + ' - ' + pac.description, pac.id,
                    sync_pac.date_deleted, sync_pac.name  + ' - ' + sync_pac.description, sync_pac.id, pac.[order]

                ORDER BY pac.[order]";
        }

        private static string Themes(Int32 db1, Int32 db2, Int32 id)
        {
            Layer layer = Layer.Theme;
            string source = App.Config["Tables:" + db1]!;
            string target = App.Config["Tables:" + db2]!;
            return $@"{DeclareQuery(db1, db2, layer, id)}

                SELECT
                    thm.id,
                    sub.name                                                    AS Subject,
                    COALESCE(pac.name + ' - ' + pac.description, '')			AS Package,
                    -1                                                          AS KnowledgeTypeId,
                    COALESCE(thm.name,'')										AS Theme,
                    {SyncedChildrenRatioQuery(layer)}                           AS SyncedChildrenRatio,
                    COALESCE(sync_sub.name, '')						            AS PairedItemSubject,
                    COALESCE(sync_pac.name  + ' - ' + sync_pac.description, '')	AS PairedItemPackage,
                    {IsSyncItemDeletedQuery(layer)}						        AS PairedItemTheme,
                    COALESCE(sync_thm.id, -1)							        AS PairedItemId



                FROM subject_type_{source} AS sub
                INNER JOIN package_{source} AS pac
                    ON sub.id = pac.id_subject_type
                    AND pac.date_deleted IS NULL
                    AND pac.id = @itemId
                INNER JOIN theme_{source} AS thm
                    ON pac.id = thm.id_package
                    AND thm.date_deleted IS NULL
                    AND thm.name NOT LIKE '[*]IMPORT[*]%'

                LEFT JOIN ({PairQuery()}) AS pair
                    ON pair.id_1 = thm.id
                LEFT JOIN theme_{target} AS sync_thm
                    ON pair.id_2 = sync_thm.id
                LEFT JOIN package_{target} AS sync_pac
                    ON sync_pac.id = sync_thm.id_package
                LEFT JOIN subject_type_{target} AS sync_sub
                    ON sync_sub.id = sync_pac.id_subject_type

                LEFT JOIN theme_part_{source} AS thm_p
                    ON thm_p.id_theme = thm.id
                LEFT JOIN knowledge_{source} AS child
                    ON child.id_theme_part = thm_p.id
                    AND child.date_deleted IS NULL

                LEFT JOIN ({PairQuery(true)}) AS sync_child
                    ON sync_child.id_1 = child.id

                GROUP BY
                    sub.id, sub.name, sync_sub.name, pac.name + ' - ' + pac.description, sync_thm.id,
                    sync_pac.name  + ' - ' + sync_pac.description, thm.name, thm.id, sync_thm.date_deleted,
                    sync_thm.name, sync_pac.id, thm.[order]

                ORDER BY thm.[order]";
        }

        private static string Knowledge(Int32 db1, Int32 db2, Int32 id)
        {
            Layer layer = Layer.Knowledge;
            string source = App.Config["Tables:" + db1]!;
            string target = App.Config["Tables:" + db2]!;
            return $@"{DeclareQuery(db1, db2, layer, id)}

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
                     {IsSyncItemDeletedQuery(layer)}						        AS PairedItemKnowledge,
                     COALESCE(sync_kno_t.name, '')						            AS PairedItemKnowledgeType,
                     COALESCE(sync_kno.id, -1)							            AS PairedItemId



                FROM subject_type_{source} AS sub
                INNER JOIN package_{source} AS pac
                    ON sub.id = pac.id_subject_type
                    AND pac.date_deleted IS NULL
                INNER JOIN theme_{source} AS thm
                    ON pac.id = thm.id_package
                    AND thm.date_deleted IS NULL
                    AND thm.id = @itemId
                INNER JOIN theme_part_{source} AS thm_p
                    ON thm_p.id_theme = thm.id
                INNER JOIN knowledge_{source} AS kno
                    ON kno.id_theme_part = thm_p.id
                    AND kno.date_deleted IS NULL
                INNER JOIN knowledge_type_{source} AS kno_t
                    ON kno_t.id = kno.id_knowledge_type

                LEFT JOIN ({PairQuery()}) AS pair
                    ON pair.id_1 = kno.id
                LEFT JOIN knowledge_{target} AS sync_kno
                    ON pair.id_2 = sync_kno.id
                LEFT JOIN theme_part_{target} AS sync_thm_p
                    ON sync_thm_p.id = sync_kno.id_theme_part
                LEFT JOIN theme_{target} AS sync_thm
                    ON sync_thm.id = sync_thm_p.id_theme
                LEFT JOIN package_{target} AS sync_pac
                    ON sync_pac.id = sync_thm.id_package
                LEFT JOIN subject_type_{target} AS sync_sub
                    ON sync_sub.id = sync_pac.id_subject_type
                LEFT JOIN knowledge_type_{target} AS sync_kno_t
                    ON sync_kno.id_knowledge_type = sync_kno_t.id

                ORDER BY kno.[order]";
        }

        private static string SpecificKnowledge(Int32 db1, Int32 db2, Int32 id)
        {
            Layer layer = Layer.KnowledgeType;
            string source = App.Config["Tables:" + db1]!;
            string target = App.Config["Tables:" + db2]!;
            return $@"{DeclareQuery(db1, db2, layer, id)}

                SELECT
                    kno.id,
                    sub.name                                                        AS Subject,
                    COALESCE(pac.name + ' - ' + pac.description, '')				AS Package,
                    COALESCE(thm.name,'')										    AS Theme,
                    COALESCE(thm_p.name, '')										AS ThemePart,
                    COALESCE(kno.knowledge_text_preview, '')						AS Knowledge,
                    COALESCE(kno_t.name, '')							            AS KnowledgeType,
                    kno_t.id											            AS KnowledgeTypeId,
                    COALESCE(sync.sub, '')						                    AS PairedItemSubject,
                    COALESCE(sync.pac, '')									        AS PairedItemPackage,
                    COALESCE(sync.thm, '')						                    AS PairedItemTheme,
                    COALESCE(sync.thm_p, '')						                AS PairedItemThemePart,
                    {IsSyncItemDeletedQuery(layer)}					                AS PairedItemKnowledge,
                    COALESCE(sync.kno_t, '')						                AS PairedItemKnowledgeType,
                    COALESCE(sync.kno_id, -1)							            AS PairedItemId



                FROM subject_type_{source} AS sub
                INNER JOIN package_{source} AS pac
                    ON sub.id = pac.id_subject_type
                    AND pac.date_deleted IS NULL
                INNER JOIN theme_{source} AS thm
                    ON pac.id = thm.id_package
                    AND thm.date_deleted IS NULL
                INNER JOIN theme_part_{source} AS thm_p
                    ON thm_p.id_theme = thm.id
                INNER JOIN knowledge_{source} AS kno
                    ON kno.id_theme_part = thm_p.id
                    AND kno.date_deleted IS NULL
                    AND kno.id = @itemId
                INNER JOIN knowledge_type_{source} AS kno_t
                    ON kno_t.id = kno.id_knowledge_type

                LEFT JOIN (
                    SELECT  sync_item_1.id_item AS pair_id, sub.name AS sub, pac.name  + ' - ' + pac.description AS pac,
                            thm.name AS thm, thm_p.name AS thm_p, kno_t.name AS kno_t, kno.id AS kno_id,
                            kno.knowledge_text_preview AS kno, kno.date_deleted AS date_deleted
                    FROM sync_pair AS sync_pair
                    INNER JOIN sync_item AS sync_item_1
                        ON (sync_pair.id_sync_item_1 = sync_item_1.id
                            OR sync_pair.id_sync_item_2 = sync_item_1.id)
                        AND sync_item_1.id_database = @dbSource
                        AND sync_item_1.id_item_type = @itemType
                        AND sync_item_1.id_item = @itemId
                    INNER JOIN sync_item AS sync_item_2
                        ON sync_item_2.id_database = @dbDest
                        AND sync_item_2.id = CASE 
							                    WHEN sync_pair.id_sync_item_1 = sync_item_1.id 
							                    THEN sync_pair.id_sync_item_2 
							                    ELSE sync_pair.id_sync_item_1 
						                    END
                    INNER JOIN knowledge_{target} AS kno
                        ON sync_item_2.id_item = kno.id
                    INNER JOIN theme_part_{target} AS thm_p
                        ON thm_p.id = kno.id_theme_part
                    INNER JOIN theme_{target} AS thm
                        ON thm.id = thm_p.id_theme
                    INNER JOIN package_{target} AS pac
                        ON pac.id = thm.id_package
                    INNER JOIN subject_type_{target} AS sub
                        ON sub.id = pac.id_subject_type
                    INNER JOIN knowledge_type_{target} AS kno_t
	                    ON kno.id_knowledge_type = kno_t.id
	            ) AS sync
	                ON sync.pair_id = kno.id";
        }

        private static string DeclareQuery(Int32 mainDatabaseId, Int32 secondaryDatabaseId, Layer layer, Int32 id)
        {
            if (layer == Layer.KnowledgeType)
                layer = Layer.Knowledge;

            return $@"  DECLARE 
                            @dbSource       CHAR(1) = '{mainDatabaseId}',
                            @dbDest         CHAR(1) = '{secondaryDatabaseId}',
                            @itemType		INT     = {(int)layer},
                            @itemId		    INT     = {id}
                    ";
        }

        private static string SelectQuery(Layer layer)
        {
            string query = $" SELECT sub.name AS sub_name ";
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

        private static string FromQuery(Layer layer, string database)
        {
            string query = $" FROM subject_type_{database} AS sub ";

            if (layer == Layer.Subject)
                return query + "WHERE sub.id = @itemId ";

            query += $"JOIN package_{database} AS pac ON sub.id = pac.id_subject_type ";

            if (layer == Layer.Package)
                return query + "WHERE pac.id = @itemId ";

            query += $"JOIN theme_{database} AS thm ON pac.id = thm.id_package ";

            if (layer == Layer.Theme)
                return query + "WHERE thm.id = @itemId ";

            return query + $@"
                            JOIN theme_part_{database} AS thm_p
                            ON thm.id = thm_p.id_theme
                            JOIN knowledge_{database} AS kno
                            ON thm_p.id = kno.id_theme_part
                            AND kno.id = @itemId
                            JOIN knowledge_type_{database} AS kno_t
                            ON kno.id_knowledge_type = kno_t.id ";
        }

        public static string OneItemQuery(Layer layer, Int32 id, Int32 databaseId)
        {
            return DeclareQuery(0, databaseId, layer, id) + SelectQuery(layer) + FromQuery(layer, App.Config["Tables:" + databaseId]!);
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
