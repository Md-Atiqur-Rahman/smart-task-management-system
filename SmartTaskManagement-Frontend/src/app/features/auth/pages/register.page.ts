import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import {
  AbstractControl,
  NonNullableFormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { SessionService } from '../../../core/auth/session.service';
import { toApiProblem } from '../../../core/http/api-problem';
import { messageFor } from '../../../core/http/error-messages';
import { applyServerErrors, controlError } from '../../../shared/forms';
import { AuthLayoutComponent } from '../components/auth-layout.component';

/** API password policy (PasswordRules): 8-128 characters with a letter and a digit. */
export const PASSWORD_MIN = 8;
export const PASSWORD_MAX = 128;

function letterAndDigit(control: AbstractControl<string>): ValidationErrors | null {
  const value = control.value ?? '';
  return value === '' || (/\p{L}/u.test(value) && /\d/.test(value))
    ? null
    : { letterAndDigit: true };
}

/** Group-level check; shown under "Confirm password". */
function passwordsMatch(group: AbstractControl): ValidationErrors | null {
  const confirm = group.get('confirmPassword')?.value as string;
  return confirm && confirm !== group.get('password')?.value ? { mismatch: true } : null;
}

/** Self-registration (prototype M01 phone 1): new accounts are always Team Members. */
@Component({
  selector: 'app-register-page',
  imports: [ReactiveFormsModule, RouterLink, AuthLayoutComponent],
  templateUrl: './register.page.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RegisterPage {
  private readonly session = inject(SessionService);
  private readonly router = inject(Router);

  protected readonly form = inject(NonNullableFormBuilder).group(
    {
      fullName: ['', [Validators.required, Validators.maxLength(100)]],
      email: ['', [Validators.required, Validators.email, Validators.maxLength(256)]],
      password: [
        '',
        [
          Validators.required,
          Validators.minLength(PASSWORD_MIN),
          Validators.maxLength(PASSWORD_MAX),
          letterAndDigit,
        ],
      ],
      confirmPassword: ['', Validators.required],
    },
    { validators: passwordsMatch },
  );

  private readonly password = toSignal(this.form.controls.password.valueChanges, {
    initialValue: '',
  });
  protected readonly rules = computed(() => {
    const value = this.password();
    return [
      {
        text: `${PASSWORD_MIN} to ${PASSWORD_MAX} characters`,
        ok: value.length >= PASSWORD_MIN && value.length <= PASSWORD_MAX,
      },
      { text: 'At least one letter', ok: /\p{L}/u.test(value) },
      { text: 'At least one digit', ok: /\d/.test(value) },
    ];
  });
  protected readonly error = signal<string | null>(null);
  protected readonly submitting = signal(false);
  protected readonly controlError = controlError;

  protected confirmError(): string | null {
    const confirm = this.form.controls.confirmPassword;
    return (
      controlError(confirm, 'Confirm password') ??
      (confirm.touched && this.form.hasError('mismatch') ? 'Passwords do not match.' : null)
    );
  }

  protected submit(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid || this.submitting()) {
      return;
    }
    this.submitting.set(true);
    this.error.set(null);
    const { fullName, email, password } = this.form.getRawValue();
    this.session.register({ fullName, email, password }).subscribe({
      next: () => void this.router.navigateByUrl('/dashboard'),
      error: (err: unknown) => {
        const problem = toApiProblem(err);
        if (problem.errorCode === 'users.email_taken') {
          this.form.controls.email.setErrors({ server: messageFor(problem) });
        } else {
          const unmatched = applyServerErrors(this.form, problem);
          this.error.set(unmatched.length ? unmatched.join(' ') : messageFor(problem));
        }
        this.submitting.set(false);
      },
    });
  }
}
