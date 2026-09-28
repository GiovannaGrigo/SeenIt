import { inject, Injectable } from "@angular/core";
import { MessageService } from "primeng/api";

@Injectable({
  providedIn: "root",
})
export class NotificationService {
  private readonly messageService = inject(MessageService);

  success(detail: string, summary = "Sucesso"): void {
    this.messageService.add({
      severity: "success",
      summary,
      detail,
      life: 3500,
    });
  }

  error(detail: string, summary = "Erro"): void {
    this.messageService.add({
      severity: "error",
      summary,
      detail,
      life: 5000,
    });
  }

  info(detail: string, summary = "Informação"): void {
    this.messageService.add({
      severity: "info",
      summary,
      detail,
      life: 3500,
    });
  }

  warn(detail: string, summary = "Atenção"): void {
    this.messageService.add({
      severity: "warn",
      summary,
      detail,
      life: 4500,
    });
  }
}
