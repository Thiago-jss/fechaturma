import { test, expect } from '@playwright/test';
import { execFile } from 'node:child_process';
import { mkdir } from 'node:fs/promises';
import { promisify } from 'node:util';
import { fileURLToPath } from 'node:url';

const base = 'http://localhost:5184';
const shots = fileURLToPath(new URL('../../artifacts/screenshots/', import.meta.url));
const runDesktop = promisify(execFile);
const desktop = fileURLToPath(new URL('../../src/FechaTurma.Desktop/bin/Release/net48/FechaTurma.Desktop.exe', import.meta.url));
const credentials = (key) => ({ Authorization: `Bearer ${key}` });

test('secretaria bloqueia a turma incompleta, coordenação fecha e a ata registra a decisão', async ({ page, request }) => {
  test.setTimeout(125_000);
  const registrar = process.env.FT_IMPORT_KEY;
  const coordinator = process.env.FT_DESKTOP_KEY;
  if (!registrar || !coordinator) throw new Error('Missing isolated demo keys.');
  await mkdir(shots, { recursive: true });
  const rows = `${base}/api/v1/closures`;
  const before = await request.get(rows);
  expect(before.status()).toBe(200);
  expect(await before.json()).toEqual([]);

  await page.goto(base);
  await expect(page.getByRole('heading', { name: 'Da pendência à ata.' })).toBeVisible();
  await page.getByRole('link', { name: /Importar uma turma/ }).click();
  await page.getByLabel('Código da turma').fill('PI-02');
  const blocked = page.waitForResponse(r => r.url().endsWith('Importacao.aspx') && r.request().method() === 'POST');
  await page.getByRole('button', { name: 'Gerar prévia' }).click();
  expect((await blocked).status()).toBe(422);
  await expect(page.getByText('Nota ausente')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Confirmar importação' })).toHaveCount(0);
  await page.getByLabel('Mostrar apenas matrículas com pendências').check();
  await expect(page.getByText('1 de 1 matrículas exibidas.')).toBeVisible();
  await page.screenshot({ path: `${shots}01-previa-bloqueada.png`, fullPage: true });

  async function preview() {
    await page.getByLabel('Código da turma').fill('PI-01');
    await page.getByRole('button', { name: 'Gerar prévia' }).click();
    await expect(page.getByText('Prévia completa.', { exact: false })).toBeVisible();
  }
  async function submit(key) {
    await page.getByLabel('Chave da secretaria').fill(key);
    const post = page.waitForResponse(r => r.url().endsWith('Importacao.aspx') && r.request().method() === 'POST');
    await page.getByRole('button', { name: 'Confirmar importação' }).click();
    return (await post).status();
  }
  await preview();
  expect(await submit(coordinator)).toBe(403);
  await preview();
  expect(await submit(registrar)).toBe(200);
  await expect(page.getByRole('status')).toContainText('Turma importada');
  await preview();
  expect(await submit(registrar)).toBe(200);
  await expect(page.getByRole('status')).toContainText('Nenhum registro foi duplicado');

  const pending = await (await request.get(rows)).json();
  expect(pending).toHaveLength(1);
  const item = pending[0];
  expect(item).toMatchObject({ classCode: 'PI-01', status: 'pending', version: 1, studentCount: 3, approvedCount: 2, failedCount: 1 });
  const detail = `${rows}/${item.id}`;
  const command = `${detail}/close`;
  const report = `${base}/atas/${item.id}`;
  expect((await request.get(report)).status()).toBe(404);
  expect((await request.post(command, { data: { version: 1 } })).status()).toBe(401);
  expect((await request.post(command, { data: { version: 1 }, headers: credentials(registrar) })).status()).toBe(403);
  expect((await request.post(command, { data: { version: 1, actor: 'other' }, headers: credentials(coordinator) })).status()).toBe(400);
  expect((await request.post(command, { data: { version: 2 }, headers: credentials(coordinator) })).status()).toBe(409);

  await page.goto(`${base}/painel/`);
  await expect(page.getByText('PI-01', { exact: false }).first()).toBeVisible();
  await expect(page.locator(`a[href="/atas/${item.id}"]`)).toHaveCount(0);
  await page.screenshot({ path: `${shots}02-painel-pendente.png`, fullPage: true });

  // Real WinForms controls select the pending record, exercise cancellation and commit only this fixture.
  await runDesktop(desktop, ['--smoke-close', item.id], { timeout: 40_000 });
  expect((await request.post(command, { data: { version: 1 }, headers: credentials(coordinator) })).status()).toBe(409);
  const closed = await (await request.get(detail)).json();
  expect(closed).toMatchObject({ status: 'closed', version: 2, studentCount: 3, failedCount: 1 });
  expect(closed.audit.map(x => [x.action, x.actor])).toEqual([['imported', 'secretaria.local'], ['closed', 'coordenacao.local']]);

  await page.reload();
  const link = page.locator(`a[href="/atas/${item.id}"]`);
  await expect(link).toBeVisible();
  await link.click();
  await expect(page.getByRole('heading', { name: 'Projeto Integrador' })).toBeVisible();
  await expect(page.getByText('coordenacao.local', { exact: false })).toBeVisible();
  await expect(page.getByRole('row')).toHaveCount(4);
  await page.screenshot({ path: `${shots}03-ata.png`, fullPage: true });
});
