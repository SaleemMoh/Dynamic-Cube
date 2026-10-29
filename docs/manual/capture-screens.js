// Captures the manual's screenshots (docs/manual/ar/images) from the running internal system in Arabic.
// Run with the dev hosts and the internal app (http://localhost:4200) up: node docs/manual/capture-screens.js docs/manual/ar/images
// Uses the development users (admin, manager) and their development password.
const { chromium } = require('playwright');
const out = process.argv[2];
const pages = [
  ['01-home', '/'],
  ['02-inbox', '/action-center'],
  ['03-supervision', '/action-center/supervision'],
  ['04-requests', '/services/requests'],
  ['05-services', '/services/catalog'],
  ['06-builder', '/services/builder?service=CLASSIFICATION_NEW'],
  ['07-questions', '/services/questions'],
  ['08-categories', '/services/categories'],
  ['09-service-versions', '/services/versions'],
  ['10-workflows', '/workflows'],
  ['11-designer', '/workflows/designer?workflowCode=CLASSIFICATION'],
  ['12-rule-sets', '/decisions'],
  ['13-rule-versions', '/decisions/versions'],
  ['14-documents', '/documents'],
  ['15-document-types', '/documents/types'],
  ['16-print-templates', '/documents/templates'],
  ['17-messages', '/communication'],
  ['18-notification-rules', '/communication/rules'],
  ['19-message-templates', '/communication/templates'],
  ['20-bills', '/payments'],
  ['21-payment-providers', '/payments/providers'],
  ['22-visits', '/visits'],
  ['23-visit-types', '/visits/types'],
  ['24-portal-home', '/content/home'],
  ['25-faqs', '/content/faqs'],
  ['26-parties', '/party/parties'],
  ['27-relationship-types', '/party/relationship-types'],
  ['28-operations', '/integration/operations'],
  ['29-test-console', '/integration/test-console'],
  ['30-call-log', '/integration/call-log'],
  ['31-change-sets', '/apps-control/change-sets'],
  ['32-classes', '/apps-control/classes'],
  ['33-lovs', '/apps-control/lovs'],
  ['34-organization', '/apps-control/organization'],
  ['35-users', '/apps-control/users'],
  ['36-portal-accounts', '/apps-control/portal-accounts'],
  ['37-roles', '/apps-control/roles'],
  ['38-menus', '/apps-control/menus'],
  ['39-settings', '/apps-control/settings'],
  ['40-installation', '/apps-control/installation'],
  ['41-branding', '/apps-control/branding'],
  ['42-approval-policy', '/apps-control/approval-policy'],
];
(async () => {
  const b = await chromium.launch();
  const ctx = await b.newContext({ locale: 'ar', viewport: { width: 1440, height: 900 } });
  const p = await ctx.newPage();
  await p.goto('http://localhost:4200/'); await p.waitForURL(/realms/);
  await p.screenshot({ path: `${out}/00-login.jpg`, type: 'jpeg', quality: 80 });
  await p.fill('#username', 'admin'); await p.fill('#password', 'Dev@12345'); await p.click('#kc-login');
  await p.waitForSelector('[data-testid=business-app]');
  const shot = (name) => p.screenshot({ path: `${out}/${name}.jpg`, type: 'jpeg', quality: 80 });
  for (const [name, url] of pages) {
    try {
      await p.goto('http://localhost:4200' + url); await p.waitForTimeout(2200);
      await shot(name);
    } catch (e) { console.log('fail', name, e.message.slice(0, 80)); }
  }
  // Dialogs and details.
  const step = async (name, fn) => { try { await fn(); await p.waitForTimeout(1200); await shot(name); await p.keyboard.press('Escape'); await p.waitForTimeout(400); } catch (e) { console.log('fail', name, e.message.slice(0, 100)); } };
  await p.goto('http://localhost:4200/services/requests'); await p.waitForTimeout(2000);
  await step('50-request-detail', async () => { await p.locator('tr.mat-mdc-row').first().click(); });
  await p.goto('http://localhost:4200/party/parties'); await p.waitForTimeout(2000);
  await step('51-party-detail', async () => { await p.locator('tr.mat-mdc-row').first().click(); });
  await p.goto('http://localhost:4200/documents/types'); await p.waitForTimeout(2000);
  await step('52-config-editor', async () => { await p.locator('[data-testid^=edit-]').first().click(); await p.waitForSelector('mat-dialog-container'); });
  await p.goto('http://localhost:4200/communication/templates'); await p.waitForTimeout(2000);
  await step('53-message-template', async () => { await p.locator('[data-testid^=edit-]').first().click(); await p.waitForSelector('mat-dialog-container'); await p.waitForTimeout(800); await p.locator('[role=tab]').nth(1).click(); });
  await p.goto('http://localhost:4200/apps-control/roles'); await p.waitForTimeout(2000);
  await step('54-role-editor', async () => { await p.locator('[data-testid^=edit-]').first().click(); await p.waitForSelector('mat-dialog-container'); });
  await p.goto('http://localhost:4200/apps-control/organization'); await p.waitForTimeout(2000);
  await step('55-org-unit', async () => { await p.click('[data-testid=org-expand]'); await p.click('[data-unit=SEC_SERVICES]'); });
  await p.goto('http://localhost:4200/apps-control/change-sets'); await p.waitForTimeout(2000);
  await step('56-change-set', async () => { await p.locator('tr.mat-mdc-row').first().click(); });
  await p.goto('http://localhost:4200/'); await p.waitForTimeout(1500);
  await step('57-app-selector', async () => { await p.click('[data-testid=business-app]'); await p.waitForTimeout(500); });
  await b.close();
  await secondPass();
})().catch((e) => { console.error(e); process.exit(1); });

// Second pass: the request manager's inbox and a task, a request and a party in detail.

async function secondPass() {
  const b = await chromium.launch();
  const login = async (user) => {
    const p = await (await b.newContext({ locale: 'ar', viewport: { width: 1440, height: 900 } })).newPage();
    await p.goto('http://localhost:4200/'); await p.waitForURL(/realms/);
    await p.fill('#username', user); await p.fill('#password', 'Dev@12345'); await p.click('#kc-login');
    await p.waitForSelector('[data-testid=business-app]'); return p;
  };
  const shot = (p, name) => p.screenshot({ path: `${out}/${name}.jpg`, type: 'jpeg', quality: 80 });
  const m = await login('manager');
  await m.goto('http://localhost:4200/action-center'); await m.waitForTimeout(2000);
  await m.locator('.dc-tile, [data-testid^=box-]').nth(1).click().catch(() => {}); await m.waitForTimeout(1500);
  await shot(m, '02-inbox');
  const row = m.locator('tr.mat-mdc-row').first();
  if (await row.count()) { await row.click(); await m.waitForTimeout(2000); await shot(m, '58-task'); }
  const a = await login('admin');
  await a.goto('http://localhost:4200/services/requests'); await a.waitForTimeout(2000);
  await a.locator('a[data-testid^=request-CLS]').first().click(); await a.waitForTimeout(2500);
  await shot(a, '50-request-detail');
  await a.goto('http://localhost:4200/party/parties'); await a.waitForTimeout(2000);
  const link = a.locator('tr.mat-mdc-row a').first();
  if (await link.count()) await link.click(); else await a.locator('tr.mat-mdc-row').first().click();
  await a.waitForTimeout(2500); await shot(a, '51-party-detail');
  await b.close();
}
