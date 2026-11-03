#!/usr/bin/env python3
"""
Builds the NHC demo package of the classification on the scoring matrix (ADR-0050):
deploy/demo-data/nhc/37-classification-matrix.json.

Input: what the matrix designer's "Import classification guide" gives for the reviewed system's April 2026 guide, i.e.
the response of POST /api/decisions/v1/matrix/import with tests/Decisions.Tests/Fixtures/ClassificationGuide/
ruleset-export.json as the body ({configuration, extras, warnings}).

    python3 deploy/demo-data/tools/build-classification-matrix.py import.json deploy/demo-data/nhc/37-classification-matrix.json

What the package holds, all through ordinary change items:
  - the bank questions the applicant answers (one per applicant input of the guide); a choice question scored the same
    way wherever it is used carries its answers' weights (points or grade) in the bank, and the matrix follows them;
  - bank questions filled by the form's integration steps (financial statements, workforce, register, violations);
  - the four integration operations (Qawaem, the sector authority, the commercial register, Ifaa) on the mock gateway;
  - the rule set CLS_MATRIX (engine MATRIX) and its first version: the imported guide, with identical cells linked to
    shared calculations, the sectors applied for read from CLS_DOMAINS, and every input wired to its question, task
    field or integration step;
  - the service CLS_MATRIX with its form (sector sections shown for the sectors chosen), integration steps and the live
    result panel, and its workflow: the matrix decision step, the financial review where the credit rating is entered
    (the grades are worked out again with it), and the general manager's approval.
"""
import copy
import json
import sys

SECTOR_ANSWERS = {
    'construction': 'CON', 'om': 'OM', 'realestate': 'RE', 'ict': 'ICT', 'cyber': 'CYB',
    'catering': 'FD', 'exhibitions': 'EX', 'survey': 'SV', 'engineering': 'ENG',
}

# Operations on the mock gateway (src/Tools/MockGateway, /v1/CLASSIFICATION-SOURCES) and the outputs the guide reads.
OPERATIONS = {
    'QAWAEM': {
        'code': 'QAWAEM_STATEMENTS', 'nameAr': 'القوائم المالية المدققة (قوائم)', 'nameEn': 'Audited financial statements (Qawaem)',
        'path': '/v1/CLASSIFICATION-SOURCES/qawaem/financial-statements/{crNumber}', 'sector': False,
        'outputs': {
            'total_cash_and_short_term_investments': 'cashAndShortTermInvestments', 'total_current_liabilities': 'currentLiabilities',
            'fiscal_year': 'fiscalYear', 'net_income': 'netIncome', 'net_property_plant_equipment': 'netPropertyPlantEquipment',
            'total_assets': 'totalAssets', 'total_equity': 'totalEquity', 'total_income': 'totalIncome',
            'total_liabilities': 'totalLiabilities', 'statements_valid': 'statementsValid',
        },
        'step': ('FINANCIAL_SOURCE', 'القوائم المالية من «قوائم»', 'Financial statements from Qawaem'),
    },
    'AUTHORITY': {
        'code': 'SECTOR_WORKFORCE', 'nameAr': 'القوى العاملة المسجلة لدى جهة القطاع', 'nameEn': 'Workforce registered with the sector authority',
        'path': '/v1/CLASSIFICATION-SOURCES/authority/workforce/{crNumber}', 'sector': True,
        'outputs': {
            'score_pct': 'scorePct', 'engineers': 'engineers', 'engineers_avg_exp': 'engineersAvgExp', 'saudi_engineers': 'saudiEngineers',
            'saudi_high_wage': 'saudiHighWage', 'saudis': 'saudis', 'saudi_women': 'saudiWomen', 'technicians': 'technicians',
            'technicians_avg_exp': 'techniciansAvgExp', 'total_workers': 'totalWorkers',
        },
        'step': ('WORKFORCE_SOURCE', 'القوى العاملة من جهة القطاع', 'Workforce from the sector authority'),
    },
    'REGISTRY': {
        'code': 'REGISTER_SECTOR_VALIDITY', 'nameAr': 'شمول السجل التجاري لنشاط القطاع', 'nameEn': 'Commercial register covers the sector',
        'path': '/v1/CLASSIFICATION-SOURCES/register/sector-validity/{crNumber}', 'sector': True,
        'outputs': {'valid_for_sector': 'validForSector'},
        'step': ('REGISTER_SOURCE', 'التحقق من السجل التجاري', 'Commercial register check'),
    },
    'IFAA': {
        'code': 'IFAA_VIOLATIONS', 'nameAr': 'المخالفات البلدية (إيفاء)', 'nameEn': 'Municipal violations (Ifaa)',
        'path': '/v1/CLASSIFICATION-SOURCES/ifaa/violations/{crNumber}', 'sector': True,
        'outputs': {'violations_in_sector': 'violationsInSector'},
        'step': ('VIOLATIONS_SOURCE', 'المخالفات البلدية من «إيفاء»', 'Municipal violations from Ifaa'),
    },
}

QUESTION_TYPES = {'number': 'NUMBER', 'text': 'TEXT', 'option': 'SELECT', 'boolean': 'CHECKBOX', 'date': 'DATE'}

# The columns of the projects question (project field codes upper-cased: the question bank's column codes).
PROJECT_COLUMNS = ['name', 'owner_name', 'owner_type', 'location', 'project_type', 'value_sar_m', 'certified_value_sar_m',
                   'completion_pct', 'start_date', 'is_withdrawn', 'withdrawn_at', 'withdrawn_by_entity']
PROJECT_EXTRA = {
    'value_sar_m': {'nameAr': 'قيمة المشروع (مليون ريال)', 'nameEn': 'Project value (SAR million)', 'type': 'number'},
    'certified_value_sar_m': {'nameAr': 'القيمة المعتمدة (مليون ريال)', 'nameEn': 'Certified value (SAR million)', 'type': 'number'},
}
PROJECT_OPTIONS = {
    'owner_type': [('gov', 'جهة حكومية', 'Government'), ('private', 'قطاع خاص', 'Private')],
    'location': [('domestic', 'داخل المملكة', 'Inside the Kingdom'), ('foreign', 'خارج المملكة', 'Outside the Kingdom')],
    'project_type': [('execution', 'تنفيذ', 'Execution'), ('om', 'تشغيل وصيانة', 'Operation & maintenance'),
                     ('design', 'تصميم', 'Design'), ('supervision', 'إشراف', 'Supervision')],
}

REVIEW_TASK = 'FIN_REVIEW'


def question_code(input_code):
    return ('MX_' + input_code.upper().replace('-', '_'))[:50]


def rename_vars(rule, names):
    """Renames the variables a JSONLogic rule reads (the first segment of each var path)."""
    if isinstance(rule, list):
        return [rename_vars(r, names) for r in rule]
    if isinstance(rule, dict):
        out = {}
        for key, value in rule.items():
            if key == 'var':
                if isinstance(value, str):
                    head, _, tail = value.partition('.')
                    out[key] = names.get(head, head) + ('.' + tail if tail else '')
                elif isinstance(value, list) and value and isinstance(value[0], str):
                    head, _, tail = value[0].partition('.')
                    out[key] = [names.get(head, head) + ('.' + tail if tail else '')] + value[1:]
                else:
                    out[key] = value
            else:
                out[key] = rename_vars(value, names)
        return out
    return rule


def fingerprint(cell):
    return json.dumps({k: cell.get(k) for k in ('kind', 'scale', 'groups', 'gradeRule', 'scoreRule', 'detailsRule')}, sort_keys=True)


def share_identical(config):
    """Links identical cells of each column to one shared calculation (as the designer's import does)."""
    shared = config.setdefault('shared', [])
    for column in config['columns']:
        groups = {}
        for cell in config['cells']:
            if cell['column'] == column['code'] and cell['kind'] in ('points', 'worst', 'formula'):
                groups.setdefault(fingerprint(cell), []).append(cell)
        for n, cells in enumerate([g for g in groups.values() if len(g) > 1], start=1):
            code = f"{column['code']}-shared-{n}"
            body = copy.deepcopy(cells[0])
            body.update({'row': '', 'column': ''})
            shared.append({'code': code, 'nameAr': f"{column['nameAr']} (مشترك)", 'nameEn': f"{column['nameEn']} (shared)", 'cell': body})
            for cell in cells:
                for key in ('scale', 'gradeRule', 'scoreRule', 'detailsRule'):
                    cell.pop(key, None)
                cell.update({'kind': 'shared', 'shared': code, 'groups': []})


def all_cells(config):
    return config['cells'] + [s['cell'] for s in config.get('shared', [])]


def main(import_path, out_path):
    imported = json.load(open(import_path, encoding='utf-8'))
    config = copy.deepcopy(imported['configuration'])
    inputs = {i['code']: i for i in config['inputs']}
    option_sets = {s['code']: s for s in config['optionSets']}
    items = []

    def add(entity_type, code, payload, operation='CREATE'):
        items.append({'entityType': entity_type, 'entityCode': code, 'operation': operation, 'payload': payload})

    # Sectors applied for: the existing domains question; each row names its answer.
    config['inputs'].append({'code': 'sectors', 'nameAr': 'القطاعات المتقدَّم لها', 'nameEn': 'Sectors applied for', 'type': 'list',
                             'source': {'kind': 'applicant', 'question': 'CLS_DOMAINS'}})
    config['rowsInput'] = 'sectors'
    for row in config['rows']:
        row['answer'] = SECTOR_ANSWERS[row['code']]

    # Projects: upper-case field codes (the question's columns) everywhere they are read.
    projects = config['projects']
    renames = {f['code']: f['code'].upper() for f in projects['fields']}
    renames.update({k: k.upper() for k in PROJECT_EXTRA})
    renames.update({f'{k}_yearsAgo': f'{v}_yearsAgo' for k, v in list(renames.items())})
    for key in ('valueField', 'certifiedValueField', 'completionField', 'dateField'):
        if projects.get(key):
            projects[key] = renames.get(projects[key], projects[key].upper())
    for field in projects['fields']:
        field['code'] = renames[field['code']]
    for code, extra in PROJECT_EXTRA.items():
        if code.upper() not in [f['code'] for f in projects['fields']]:
            projects['fields'].append({'code': code.upper(), **extra, 'source': {'kind': 'given'}})
    for rule in projects['rules']:
        if rule.get('matches') is not None:
            rule['matches'] = rename_vars(rule['matches'], renames)
    for i in config['inputs']:
        if i['source']['kind'] == 'projects':
            if i['source'].get('column'):
                i['source']['column'] = renames.get(i['source']['column'], i['source']['column'].upper())
            if i['source'].get('where') is not None:
                i['source']['where'] = rename_vars(i['source']['where'], renames)

    # Weights in the bank: a choice input scored by answer the same way everywhere carries them on its question.
    cells_by_input = {}
    for cell in config['cells']:
        for group in cell.get('groups', []):
            for criterion in group['criteria']:
                cells_by_input.setdefault(criterion['input'], []).append(criterion)
    bank_weights = {}
    for code, criteria in cells_by_input.items():
        scored = [c for c in criteria if c['scoring'] == 'options']
        if not scored or len(scored) != len(criteria):
            continue
        outputs = {c['output'] for c in scored}
        maps = {json.dumps(sorted((o['value'], o.get('points'), o.get('grade')) for o in c['options'])) for c in scored}
        if len(outputs) == 1 and len(maps) == 1:
            bank_weights[code] = (outputs.pop(), {o['value']: o for o in scored[0]['options']})
            for c in scored:
                c['fromBank'] = True

    # The projects list is answered by the applicant (a rows question).
    if 'projects' not in inputs:
        inputs['projects'] = {'code': 'projects', 'nameAr': 'المشاريع', 'nameEn': 'Projects', 'type': 'list', 'source': {'kind': 'applicant'}}
        config['inputs'].append(inputs['projects'])
    inputs['projects']['source'] = {'kind': 'applicant', 'question': 'projects'}

    # Bank questions the applicant answers.
    for i in list(inputs.values()):
        if i['source']['kind'] != 'applicant':
            continue
        if i['code'] == 'projects':
            qcode = 'MX_PROJECTS'
            columns = []
            for field in PROJECT_COLUMNS:
                f = next(x for x in projects['fields'] if x['code'] == field.upper())
                col = {'code': f['code'], 'labelAr': f['nameAr'], 'labelEn': f['nameEn'],
                       'type': 'CHECKBOX' if f['type'] == 'boolean' else QUESTION_TYPES.get(f['type'], 'TEXT'),
                       'required': field in ('name', 'owner_type', 'location', 'project_type', 'value_sar_m', 'completion_pct', 'start_date'),
                       'inTable': field in ('name', 'project_type', 'value_sar_m', 'completion_pct')}
                if field in PROJECT_OPTIONS:
                    col['type'] = 'SELECT'
                    col['options'] = [{'value': v, 'labelAr': a, 'labelEn': e} for v, a, e in PROJECT_OPTIONS[field]]
                columns.append(col)
            add('svc.question', qcode, {'nameAr': 'مشاريع المنشأة', 'nameEn': "The establishment's projects", 'type': 'ROWS',
                                        'columns': columns, 'validation': {'maxRows': 50},
                                        'hintAr': 'المشاريع المنفذة أو الجارية؛ تُحتسب وفق شروط الإنجاز والعمر والقيمة.',
                                        'hintEn': 'Delivered or running projects; they count by completion, age and value.'})
            i['source']['question'] = qcode
            continue
        qcode = question_code(i['code'])
        qtype = 'TEXTAREA' if i['type'] == 'list' else QUESTION_TYPES.get(i['type'], 'TEXT')
        if i['type'] == 'list':
            i['type'] = 'text'
        payload = {'nameAr': i['nameAr'] or i['code'], 'nameEn': i['nameEn'] or i['code'], 'type': qtype, 'validation': {}}
        if i.get('min') is not None:
            payload['validation']['min'] = i['min']
        if i.get('max') is not None:
            payload['validation']['max'] = i['max']
        if i['type'] == 'option' and i.get('optionSet') in option_sets:
            weights = bank_weights.get(i['code'])
            options = []
            for item in option_sets[i['optionSet']]['items']:
                option = {'value': item['code'], 'labelAr': item['labelAr'], 'labelEn': item['labelEn']}
                if weights and item['code'] in weights[1]:
                    scored = weights[1][item['code']]
                    if weights[0] == 'points' and scored.get('points') is not None:
                        option['points'] = scored['points']
                    if weights[0] == 'grade' and scored.get('grade') is not None:
                        option['grade'] = scored['grade']
                options.append(option)
            payload['options'] = options
            if weights:
                payload['hintAr'] = 'للإجابات أوزان في التقييم'
                payload['hintEn'] = 'Its answers carry scoring weights'
        add('svc.question', qcode, payload)
        i['source']['question'] = qcode

    # Integration operations and the questions their outputs fill.
    for guide_op, spec in OPERATIONS.items():
        parameters = [{'name': 'crNumber', 'location': 'PATH', 'isRequired': True, 'isMasked': False, 'sampleValue': '1010000001'}]
        if spec['sector']:
            parameters.append({'name': 'sector', 'location': 'QUERY', 'isRequired': False, 'isMasked': False, 'sampleValue': 'CON'})
        add('integ.operation', spec['code'], {
            'nameAr': spec['nameAr'], 'nameEn': spec['nameEn'], 'connectionCode': 'MOMRAH_GATEWAY', 'envelopeProfileCode': 'MOMRAH',
            'method': 'GET', 'pathTemplate': spec['path'], 'parameters': parameters,
            'outputs': [{'name': name, 'path': path} for name, path in spec['outputs'].items()],
            'responseCodes': [
                {'code': '006-000001', 'outcome': 'NOT_FOUND', 'messageAr': 'لا توجد بيانات لدى الجهة.', 'messageEn': 'The source has no data.'},
                {'code': '006-000002', 'outcome': 'BUSINESS_ERROR', 'messageAr': 'رقم السجل التجاري غير صحيح.', 'messageEn': 'Wrong CR number.'},
            ],
            'maskFields': [], 'cacheSeconds': 300, 'businessApp': 'CLASSIFICATION'})
    filled = {}
    for i in list(inputs.values()):
        if i['source']['kind'] != 'integration':
            continue
        spec = OPERATIONS[i['source']['operation']]
        qcode = question_code(i['code'])
        add('svc.question', qcode, {'nameAr': i['nameAr'], 'nameEn': i['nameEn'],
                                    'type': 'CHECKBOX' if i['type'] == 'boolean' else 'NUMBER', 'validation': {},
                                    'hintAr': f"من {spec['nameAr']}", 'hintEn': f"From {spec['nameEn']}"})
        filled.setdefault(i['source']['operation'], []).append((i['source']['output'], qcode, i))
        i['source'].update({'operation': spec['code'], 'question': qcode, 'allowOverride': True})

    # The reviewer's input: a field of the financial review task.
    for i in inputs.values():
        if i['source']['kind'] == 'reviewer':
            i['source'].update({'path': 'CLS_MATRIX', 'step': REVIEW_TASK, 'field': i['code'].upper()})

    share_identical(config)
    add('dec.rule-set', 'CLS_MATRIX', {'nameAr': 'مصفوفة التصنيف (دليل أبريل 2026)', 'nameEn': 'Classification matrix (April 2026 guide)',
                                       'engine': 'MATRIX', 'businessApp': 'CLASSIFICATION',
                                       'descriptionAr': 'القطاعات صفوف والمسارات أعمدة؛ تقرأ إجابات الطلب وحقول المراجعة ونتائج التكامل بنفسها.',
                                       'descriptionEn': "Sectors are rows and tracks are columns; it reads the request's answers, review fields and integration results itself."})
    add('dec.rule-set-version', 'CLS_MATRIX/1', {'nameAr': 'الإصدار 1', 'nameEn': 'Version 1', 'configuration': config})

    # The service, its form and the live result.
    add('svc.service', 'CLS_MATRIX', {
        'nameAr': 'تصنيف المقاولين (مصفوفة التقييم)', 'nameEn': 'Contractor classification (scoring matrix)',
        'descriptionAr': 'تصنيف المنشأة في قطاع أو أكثر وفق الدليل الاسترشادي (أبريل 2026)، بدرجة تظهر مباشرة أثناء التعبئة.',
        'descriptionEn': 'Classifies the establishment in one or more sectors under the April 2026 guide, with the grade shown live while filling.',
        'archetype': 'ISSUANCE', 'categoryCode': 'CLASSIFICATION', 'icon': 'grid_on', 'displayOrder': 15, 'partyKinds': ['ESTABLISHMENT'],
        'isListed': True, 'draftsAllowed': True, 'draftExpiryDays': 30, 'onNewVersion': 'CONTINUE', 'numberPrefix': 'CLM',
        'businessApp': 'CLASSIFICATION'})

    sections = {s['code']: s for s in imported['extras'].get('formSections', [])}
    placed = {}
    for row in config['rows']:
        for field in (row.get('settings') or {}).get('form', []):
            code = field['input']
            if code not in inputs or inputs[code]['source']['kind'] != 'applicant' or code == 'projects':
                continue
            entry = placed.setdefault(code, {'sectors': set(), 'section': field['section'], 'order': field['order'], 'required': False, 'visibleWhen': field.get('visibleWhen')})
            entry['sectors'].add(row['answer'])
            entry['required'] = entry['required'] or bool(field.get('required'))
            entry['order'] = min(entry['order'], field['order'])
    question_names = {i['code']: i['source']['question'] for i in inputs.values() if i['source'].get('question')}
    all_sectors = set(SECTOR_ANSWERS.values())
    steps = [
        {'code': 'LEVEL', 'type': 'LEVEL', 'titleAr': 'مسار الخدمة', 'titleEn': 'Service track', 'sections': []},
        {'code': 'DOMAINS', 'type': 'FORM', 'titleAr': 'القطاعات', 'titleEn': 'Sectors', 'sections': [
            {'code': 'DOMAINS', 'titleAr': 'القطاعات المطلوب التصنيف فيها', 'titleEn': 'Sectors to be classified in', 'questions': [
                {'code': 'CLS_DOMAINS', 'required': True}, {'code': 'CLS_PRIMARY_DOMAIN', 'required': True}]}]},
    ]
    for guide_op, spec in OPERATIONS.items():
        code, title_ar, title_en = spec['step']
        inputs_map = [{'parameter': 'crNumber', 'from': 'party.identifiers.CR_NUMBER'}]
        if spec['sector']:
            inputs_map.append({'parameter': 'sector', 'from': 'data.CLS_PRIMARY_DOMAIN'})
        outs = filled.get(guide_op, [])
        steps.append({'code': code, 'type': 'INTEGRATION', 'titleAr': title_ar, 'titleEn': title_en, 'sections': [],
                      'textAr': f"نجلب البيانات من {spec['nameAr']}؛ تدخل مباشرة في احتساب الدرجة.",
                      'textEn': f"We fetch the data from {spec['nameEn']}; it goes straight into the grade.",
                      'integration': {'operation': spec['code'], 'inputs': inputs_map, 'required': False, 'showResult': True,
                                      'outputs': [{'output': o, 'question': q} for o, q, _ in outs],
                                      'display': [{'output': o, 'labelAr': i['nameAr'], 'labelEn': i['nameEn']} for o, _, i in outs[:8]]}})
    # The fetched values are shown read-only (a step's outputs must be questions the version asks).
    steps.append({'code': 'SOURCES_DATA', 'type': 'FORM', 'titleAr': 'البيانات من الجهات', 'titleEn': 'Data from the sources', 'sections': [
        {'code': spec['step'][0], 'titleAr': spec['nameAr'], 'titleEn': spec['nameEn'], 'columns': 2,
         'questions': [{'code': q, 'required': False, 'readonly': True} for _, q, _ in filled.get(guide_op, [])]}
        for guide_op, spec in OPERATIONS.items() if filled.get(guide_op)]})
    by_section = {}
    for code, entry in sorted(placed.items(), key=lambda kv: kv[1]['order']):
        use = {'code': question_names[code], 'required': entry['required']}
        conditions = []
        if entry['sectors'] != all_sectors:
            conditions.append({'or': [{'in': [s, {'var': 'CLS_DOMAINS'}]} for s in sorted(entry['sectors'])]})
        if entry['visibleWhen'] is not None:
            conditions.append(rename_vars(entry['visibleWhen'], question_names))
        if conditions:
            use['visibleWhen'] = conditions[0] if len(conditions) == 1 else {'and': conditions}
        by_section.setdefault(entry['section'], []).append(use)
    for section in sorted(by_section, key=lambda s: sections.get(s, {}).get('sortOrder', 99)):
        meta = sections.get(section, {'nameAr': section, 'nameEn': section})
        steps.append({'code': section.upper(), 'type': 'FORM', 'titleAr': meta['nameAr'], 'titleEn': meta['nameEn'], 'sections': [
            {'code': section.upper(), 'titleAr': meta['nameAr'], 'titleEn': meta['nameEn'], 'columns': 2, 'questions': by_section[section]}]})
    steps.append({'code': 'PROJECTS', 'type': 'FORM', 'titleAr': 'المشاريع', 'titleEn': 'Projects', 'sections': [
        {'code': 'PROJECTS', 'titleAr': 'مشاريع المنشأة', 'titleEn': "The establishment's projects",
         'questions': [{'code': 'MX_PROJECTS', 'required': False}]}]})
    steps += [{'code': 'REVIEW', 'type': 'REVIEW', 'titleAr': 'مراجعة الطلب', 'titleEn': 'Review', 'sections': []},
              {'code': 'DECLARATION', 'type': 'DECLARATION', 'titleAr': 'الإقرار', 'titleEn': 'Declaration', 'sections': [],
               'textAr': 'أقر بصحة البيانات المقدمة.', 'textEn': 'I declare the data given is correct.'}]
    scale = [{'value': str(g['rank']), 'labelAr': g['nameAr'], 'labelEn': g['nameEn']} for g in config['grades']]
    definition = {
        'levels': [{'code': 'NORMAL', 'nameAr': 'المسار العادي', 'nameEn': 'Normal track', 'descriptionAr': 'بدون رسوم', 'descriptionEn': 'Free of charge', 'slaWorkingDays': 30}],
        'steps': steps, 'eligibility': [], 'validations': [
            {'code': 'PRIMARY_IN_DOMAINS', 'step': 'DOMAINS', 'rule': {'in': [{'var': 'data.CLS_PRIMARY_DOMAIN'}, {'var': 'data.CLS_DOMAINS'}]},
             'messageAr': 'يجب أن يكون القطاع الرئيسي من القطاعات المختارة.', 'messageEn': 'The primary sector must be one of the chosen sectors.'}],
        'fees': [],
        'card': {'audienceAr': 'المقاولون والمكاتب الهندسية في القطاعات التسعة', 'audienceEn': 'Contractors and engineering offices in the nine sectors',
                 'steps': [{'textAr': 'اختر القطاعات', 'textEn': 'Choose the sectors'},
                           {'textAr': 'تُجلب بياناتك المالية والقوى العاملة تلقائياً', 'textEn': 'Your financial and workforce data are fetched for you'},
                           {'textAr': 'أجب عن أسئلة كل قطاع وتابع درجتك مباشرة', 'textEn': "Answer each sector's questions and follow your grade live"}]},
        'panel': {
            'readiness': {'enabled': True},
            'result': {
                'enabled': True, 'titleAr': 'الدرجة المتوقعة لكل قطاع', 'titleEn': 'Expected grade per sector', 'step': 'GRADE',
                'scale': scale, 'unknownAr': 'غير مصنف', 'unknownEn': 'Not graded',
                'values': [{'output': 'size', 'labelAr': 'حجم المنشأة', 'labelEn': 'Establishment size', 'onScale': False}],
                'items': {'output': 'items', 'titleOutput': 'nameAr', 'titleOutputEn': 'nameEn', 'resultOutput': 'expectedGrade',
                          'columns': [{'output': c['code'], 'labelAr': c['nameAr'], 'labelEn': c['nameEn'], 'onScale': True} for c in config['columns']]},
            },
        },
    }
    add('svc.service-version', 'CLS_MATRIX/1', {'nameAr': 'الإصدار 1', 'nameEn': 'Version 1', 'definition': definition})

    # The workflow: grade, financial review (credit rating, graded again), general manager's approval.
    rating = next(i for i in inputs.values() if i['source']['kind'] == 'reviewer')
    rating_options = [{'value': o['code'], 'labelAr': o['labelAr'], 'labelEn': o['labelEn']} for o in option_sets[rating['optionSet']]['items']]
    nodes = [
        {'code': 'START', 'type': 'START', 'nameAr': 'البداية', 'nameEn': 'Start', 'next': 'GRADE'},
        {'code': 'GRADE', 'type': 'DECISION', 'nameAr': 'احتساب الدرجات بالمصفوفة', 'nameEn': 'Grade with the matrix',
         'ruleSet': 'CLS_MATRIX', 'result': 'grading', 'input': {}, 'next': REVIEW_TASK},
        {'code': REVIEW_TASK, 'type': 'HUMAN_TASK', 'nameAr': 'المراجعة المالية', 'nameEn': 'Financial review',
         'assignment': {'target': 'ROLE', 'role': 'CLS_FIN_ANALYST', 'mode': 'QUEUE'},
         'actions': [{'code': 'CONFIRM', 'kind': 'COMPLETE', 'nameAr': 'اعتماد المراجعة', 'nameEn': 'Confirm the review', 'to': 'GM_APPROVAL'},
                     {'code': 'RETURN', 'kind': 'RETURN', 'nameAr': 'طلب بيانات إضافية', 'nameEn': 'Ask for more data'}],
         'fields': [{'code': rating['code'].upper(), 'labelAr': rating['nameAr'], 'labelEn': rating['nameEn'], 'type': 'SELECT', 'required': True,
                     'options': rating_options, 'hintAr': 'من تقرير الائتمان المرفق؛ تُعاد الدرجات به', 'hintEn': 'From the attached credit report; the grades are worked out again with it'}],
         'recalculate': ['GRADE']},
        {'code': 'GM_APPROVAL', 'type': 'HUMAN_TASK', 'nameAr': 'اعتماد المدير العام', 'nameEn': "General manager's approval",
         'assignment': {'target': 'ROLE', 'role': 'CLS_GENERAL_MANAGER', 'mode': 'QUEUE'},
         'actions': [{'code': 'APPROVE', 'kind': 'APPROVE', 'nameAr': 'اعتماد التصنيف', 'nameEn': 'Approve the classification', 'commentToApplicant': True, 'to': 'APPROVED'},
                     {'code': 'REJECT', 'kind': 'REJECT', 'nameAr': 'رفض', 'nameEn': 'Reject', 'commentToApplicant': True, 'to': 'REJECTED'}]},
        {'code': 'APPROVED', 'type': 'END', 'nameAr': 'معتمد', 'nameEn': 'Approved', 'outcome': 'APPROVED'},
        {'code': 'REJECTED', 'type': 'END', 'nameAr': 'مرفوض', 'nameEn': 'Rejected', 'outcome': 'REJECTED'},
    ]
    add('wf.workflow', 'CLS_MATRIX', {'nameAr': 'معالجة طلبات التصنيف بالمصفوفة', 'nameEn': 'Matrix classification processing',
                                       'descriptionAr': 'احتساب الدرجات بالمصفوفة من بيانات الطلب والجهات؛ المراجعة المالية وإدخال التقييم الائتماني (تُعاد الدرجات)؛ اعتماد المدير العام.',
                                       'descriptionEn': "Grades worked out by the matrix from the request and the sources; the financial review enters the credit rating (grades worked out again); the general manager's approval.",
                                       'serviceCode': 'CLS_MATRIX'},
        operation='UPDATE')
    add('wf.workflow-version', 'CLS_MATRIX/2', {'nameAr': 'الإصدار 2', 'nameEn': 'Version 2', 'definition': {'nodes': nodes}})

    package = {
        'title': 'NHC — classification on the scoring matrix: the April 2026 guide imported, governance and other identical '
                 'tracks shared, answers weighted in the question bank, data fetched from Qawaem, the sector authority, '
                 'the register and Ifaa, and the credit rating entered in the financial review',
        'items': items,
    }
    json.dump(package, open(out_path, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    shared = sum(1 for c in config['cells'] if c['kind'] == 'shared')
    print(f"{len(items)} items: {sum(1 for i in items if i['entityType'] == 'svc.question')} questions, "
          f"{len(bank_weights)} weighted in the bank, {shared} cells linked to {len(config['shared'])} shared calculations")


if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2])
