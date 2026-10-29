import { WritableSignal, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { FileUploader, UploadedFile } from '../forms/file-input';
import { RequestFileInfo } from './service.models';
import { ServicesApi } from './services.api';

/** Opens a downloaded file in a new tab (PDFs and images show; other types download). */
export function openBlob(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.target = '_blank';
  link.rel = 'noopener';
  if (!/^(application\/pdf|image\/)/.test(blob.type)) link.download = fileName;
  link.click();
  setTimeout(() => URL.revokeObjectURL(url), 60_000);
}

/** Opens a file of a request: the applicant's own on the portal, a submitted request's for employees. */
export async function openRequestFile(api: ServicesApi, requestId: string, file: Pick<RequestFileInfo, 'id' | 'fileName'>, staff = false): Promise<void> {
  openBlob(await firstValueFrom(api.downloadFile(requestId, file.id, staff)), file.fileName);
}

/** The portal's uploader: files go to the request (and into the answer on the server); `files` follows them. */
export function requestFileUploader(api: ServicesApi, requestId: () => string, files: WritableSignal<RequestFileInfo[]>): FileUploader {
  return {
    files: () => files(),
    async upload(field, file) {
      const uploaded = await firstValueFrom(api.uploadFile(requestId(), field, file));
      // A one-file question's old file is removed on the server; its id leaves the answer, so it is no longer shown.
      files.update((list) => [...list, uploaded]);
      return uploaded;
    },
    async remove(_field, id) {
      await firstValueFrom(api.deleteFile(requestId(), id));
      files.update((list) => list.filter((f) => f.id !== id));
    },
    open(id) {
      const file = files().find((f) => f.id === id);
      if (file) void openRequestFile(api, requestId(), file);
    },
  };
}

/** A preview's uploader: files stay in the browser (nothing is sent). */
export function browserFileUploader(): FileUploader {
  const kept = signal<(UploadedFile & { url: string })[]>([]);
  return {
    files: () => kept(),
    async upload(_field, file) {
      const entry = {
        id: `local-${kept().length + 1}-${file.name}`,
        fileName: file.name,
        length: file.size,
        contentType: file.type,
        url: URL.createObjectURL(file),
      };
      kept.update((list) => [...list, entry]);
      return entry;
    },
    async remove(_field, id) {
      const entry = kept().find((f) => f.id === id);
      if (entry) URL.revokeObjectURL(entry.url);
      kept.update((list) => list.filter((f) => f.id !== id));
    },
    open(id) {
      const entry = kept().find((f) => f.id === id);
      if (entry) window.open(entry.url, '_blank', 'noopener');
    },
  };
}
