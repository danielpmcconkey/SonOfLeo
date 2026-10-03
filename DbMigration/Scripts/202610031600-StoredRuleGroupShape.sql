-- Classification rule groups are stored in a fixed shape of their own (REQ-CR-1.13), not the serialised domain types,
-- so the stored JSON no longer changes when the code's types do. A group becomes
--   {"connector": "And", "chainOne": [field match, ...], "chainTwo": [field match, ...] or null}
-- and a field match becomes
--   {"field": "Source", "pattern": "...", "lineType": null, "numericOperator": null, "amount": null}
-- with only the values its field uses set.
--
-- Memo is no longer a match target (REQ-CR-2.2 withdrawn). A rule that still matches on memo cannot be converted
-- without changing what it matches, which is a decision for a person, so this script stops and names those rules.

DO $$
DECLARE
    memo_rules text;
BEGIN
    SELECT string_agg(DISTINCT cr.rule_name, ', ')
    INTO memo_rules
    FROM classification.classification_rule cr,
         jsonb_array_elements(cr.rule_groups) AS rg,
         jsonb_array_elements(
            (rg.value -> 'chainOne' -> 'chain')
            || COALESCE(NULLIF(rg.value -> 'chainTwo', 'null'::jsonb) -> 'chain', '[]'::jsonb)
         ) AS fm
    WHERE fm.value ->> 'Case' = 'Memo';

    IF memo_rules IS NOT NULL THEN
        RAISE EXCEPTION 'Cannot convert classification rules: these rules match on memo, which is no longer a match target: %. Rewrite them without the memo match, then run this migration again.', memo_rules;
    END IF;
END $$;

CREATE FUNCTION pg_temp.stored_chain(chain jsonb) RETURNS jsonb AS $$
    SELECT COALESCE(jsonb_agg(
        jsonb_build_object(
            'field', fm.value ->> 'Case',
            'pattern', CASE WHEN fm.value ->> 'Case' IN ('Source', 'Description') THEN fm.value -> 'Fields' ->> 0 END,
            'lineType', CASE WHEN fm.value ->> 'Case' = 'LineType' THEN fm.value -> 'Fields' -> 0 ->> 'Case' END,
            'numericOperator',
                CASE WHEN fm.value ->> 'Case' = 'Amount'
                     THEN fm.value -> 'Fields' -> 0 -> 'numericSearchOperator' ->> 'Case' END,
            'amount',
                CASE WHEN fm.value ->> 'Case' = 'Amount'
                     THEN fm.value -> 'Fields' -> 0 -> 'amount' -> 'amount' END)
        ORDER BY fm.ordinality), '[]'::jsonb)
    FROM jsonb_array_elements(chain -> 'chain') WITH ORDINALITY AS fm
$$ LANGUAGE sql;

UPDATE classification.classification_rule cr
SET rule_groups = (
    SELECT jsonb_agg(
        jsonb_build_object(
            'connector', rg.value -> 'connector' ->> 'Case',
            'chainOne', pg_temp.stored_chain(rg.value -> 'chainOne'),
            'chainTwo',
                CASE WHEN rg.value -> 'chainTwo' IS NULL OR rg.value -> 'chainTwo' = 'null'::jsonb
                     THEN 'null'::jsonb
                     ELSE pg_temp.stored_chain(rg.value -> 'chainTwo') END)
        ORDER BY rg.ordinality)
    FROM jsonb_array_elements(cr.rule_groups) WITH ORDINALITY AS rg)
-- a rule already in the stored shape has a string connector; leave it alone so the script can run twice
WHERE jsonb_typeof(cr.rule_groups -> 0 -> 'connector') = 'object';
