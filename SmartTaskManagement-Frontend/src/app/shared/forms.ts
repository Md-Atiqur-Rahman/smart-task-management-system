import { AbstractControl, FormGroup } from '@angular/forms';
import { ApiProblem } from '../core/http/api-problem';

/**
 * Puts server validation messages (400 validation.failed) on the matching form
 * controls. Returns the messages that matched no control, for a form-level alert.
 */
export function applyServerErrors(form: FormGroup, problem: ApiProblem): string[] {
  const unmatched: string[] = [];
  for (const [field, messages] of Object.entries(problem.fieldErrors)) {
    const control = form.get(field);
    if (control) {
      control.setErrors({ server: messages.join(' ') });
      control.markAsTouched();
    } else {
      unmatched.push(...messages);
    }
  }
  return unmatched;
}

/** First error text for a control, or null while it is untouched or valid. */
export function controlError(control: AbstractControl, label: string): string | null {
  if (!control.touched || !control.errors) {
    return null;
  }
  const errors = control.errors;
  if (errors['server']) return String(errors['server']);
  if (errors['required']) return `${label} is required.`;
  if (errors['email']) return 'Enter a valid email address.';
  if (errors['maxlength']) {
    return `${label} must be at most ${(errors['maxlength'] as { requiredLength: number }).requiredLength} characters.`;
  }
  if (errors['minlength']) {
    return `${label} must be at least ${(errors['minlength'] as { requiredLength: number }).requiredLength} characters.`;
  }
  if (errors['letterAndDigit']) return `${label} must contain at least one letter and one digit.`;
  if (errors['mismatch']) return 'Passwords do not match.';
  return `${label} is not valid.`;
}
